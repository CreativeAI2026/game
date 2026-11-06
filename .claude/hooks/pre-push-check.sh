#!/usr/bin/env bash
# Claude が git push する前に、チームのルールを満たしているか確認する。満たさなければ push を止める。
input="$(cat)"
if ! printf '%s' "$input" | grep -qE '"command"[[:space:]]*:[[:space:]]*"[^"]*git[[:space:]]+push'; then
  exit 0
fi

cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0
errors=()

branch="$(git rev-parse --abbrev-ref HEAD)"
if [[ "$branch" == "main" ]]; then
  errors+=("main へ直接 push しない。ブランチを切って PR にする。")
elif [[ ! "$branch" =~ ^(feature|fix|docs|chore)/[a-z0-9-]+$ ]]; then
  errors+=("ブランチ名 '$branch' が <種別>/<内容> の形式（feature|fix|docs|chore、英小文字とハイフン）になっていない。git branch -m で付け直す。")
fi

if git fetch origin main --quiet; then
  if ! git merge-base --is-ancestor origin/main HEAD; then
    errors+=("最新の main を取り込んでいない。git rebase origin/main を実行する。")
  fi
  if [[ -n "$(git rev-list --merges origin/main..HEAD)" ]]; then
    errors+=("ブランチに merge コミットがある。merge ではなく rebase で main を取り込む。")
  fi
else
  errors+=("git fetch origin main に失敗した。")
fi

if command -v mise > /dev/null; then
  if ! out="$(mise exec -- dotnet csharpier check . 2>&1)"; then
    errors+=("CSharpier の整形漏れがある。mise exec -- dotnet csharpier format . を実行する。"$'\n'"$out")
  fi
else
  errors+=("mise が見つからない。docs/MiseSetup.md の手順で入れる。")
fi

if ! out="$(bash .github/scripts/check-meta.sh 2>&1)"; then
  errors+=(".meta の欠落か孤児がある。"$'\n'"$out")
fi

if ((${#errors[@]})); then
  {
    echo "push 前のチェックに失敗したので push を止めた:"
    for e in "${errors[@]}"; do echo "- $e"; done
  } >&2
  exit 2
fi
