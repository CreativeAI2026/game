#!/usr/bin/env bash
# Assets/ 配下の .meta の欠落と孤児を検出する（git の追跡ファイルが対象）。
set -euo pipefail
export LC_ALL=C

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

git -c core.quotePath=false ls-files -- 'Assets/' > "$tmp/all"

if [[ ! -s "$tmp/all" ]]; then
  echo "Assets/ 配下に追跡ファイルがありません。スキップします。"
  exit 0
fi

grep -v '\.meta$' "$tmp/all" > "$tmp/files" || true
awk -F/ '{
  path=""
  for (i = 1; i < NF; i++) {
    path = (i == 1) ? $i : path "/" $i
    if (path != "Assets") print path
  }
}' "$tmp/all" | sort -u > "$tmp/dirs"
cat "$tmp/files" "$tmp/dirs" | sort -u > "$tmp/assets"

grep '\.meta$' "$tmp/all" | sed 's/\.meta$//' | sort -u > "$tmp/metas"

errors=0

while IFS= read -r a; do
  [[ -z "$a" ]] && continue
  echo "::error::Missing .meta for: $a"
  errors=$((errors + 1))
done < <(comm -23 "$tmp/assets" "$tmp/metas")

# git は空フォルダを追跡しないので、フォルダの .meta だけが残っているのは許容する。
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
