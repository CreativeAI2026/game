#!/usr/bin/env bash
#
# Unity .meta 整合チェック
#   1. Assets/ 配下の全アセット（ファイル）に対応する .meta があるか
#   2. アセットを含む全フォルダに対応する .meta があるか
#   3. 全 .meta に対応するアセット（ファイル / フォルダ）があるか（孤児 .meta 検出）
#
# git の追跡対象のみを対象にする（ローカルの未追跡ファイルは無視）。
# macOS の bash 3.2 でも動くよう、連想配列を使わず sort/comm で集合演算する。
set -euo pipefail
export LC_ALL=C   # sort と comm の照合順序を一致させる

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

git ls-files -- 'Assets/' > "$tmp/all"

if [[ ! -s "$tmp/all" ]]; then
  echo "Assets/ 配下に追跡ファイルがありません。スキップします。"
  exit 0
fi

# .meta を要求するアセット = 非 .meta ファイル + それらを含むディレクトリ（Assets 自身は除く）
grep -v '\.meta$' "$tmp/all" > "$tmp/files" || true
awk -F/ '{
  path=""
  for (i = 1; i < NF; i++) {
    path = (i == 1) ? $i : path "/" $i
    if (path != "Assets") print path
  }
}' "$tmp/all" | sort -u > "$tmp/dirs"
cat "$tmp/files" "$tmp/dirs" | sort -u > "$tmp/assets"

# .meta が指すアセットパス（末尾 .meta を除去）
grep '\.meta$' "$tmp/all" | sed 's/\.meta$//' | sort -u > "$tmp/metas"

errors=0

# 1 & 2: アセットにあるが .meta が無い
while IFS= read -r a; do
  [[ -z "$a" ]] && continue
  echo "::error::Missing .meta for: $a"
  errors=$((errors + 1))
done < <(comm -23 "$tmp/assets" "$tmp/metas")

# 3: .meta はあるが対応アセットが無い（孤児）
#    ただし「空フォルダのフォルダ .meta（folderAsset: yes）」は許容する。
#    git は空ディレクトリを追跡しないため、構造維持目的でフォルダ .meta のみ
#    コミットされている状態は正常系として扱う。危険なのはファイルの孤児 .meta。
skipped_folders=0
while IFS= read -r a; do
  [[ -z "$a" ]] && continue
  metafile="${a}.meta"
  if [[ -f "$metafile" ]] && grep -q '^folderAsset: yes' "$metafile"; then
    skipped_folders=$((skipped_folders + 1))
    continue
  fi
  echo "::error::Orphan .meta (no matching asset): ${metafile}"
  errors=$((errors + 1))
done < <(comm -13 "$tmp/assets" "$tmp/metas")

if [[ "$skipped_folders" -gt 0 ]]; then
  echo "ℹ️ ${skipped_folders} 件の空フォルダ .meta（folderAsset）は許容としてスキップしました。"
fi

if [[ "$errors" -gt 0 ]]; then
  echo ""
  echo "❌ .meta check failed: ${errors} issue(s)."
  exit 1
fi

echo "✅ All .meta files are consistent."
