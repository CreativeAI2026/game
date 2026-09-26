# 開発フロー

変更はブランチを切って PR 経由で `main` に入れる（`main` への直 push は禁止）。

---

## ブランチ名

`<種別>/<内容>` の形式。`<内容>` は英小文字 + ハイフン。

| 種別 | 用途 | 例 |
|------|------|-----|
| `feature/` | 機能追加 | `feature/player-move-system` |
| `fix/` | バグ修正 | `fix/enemy-spawn-null` |
| `docs/` | ドキュメント | `docs/dev-onboarding` |
| `chore/` | 設定・雑務 | `chore/update-gitignore` |

---

## コミットメッセージ

- **日本語で書く**。
- 「何をしたか」が分かる簡潔な 1 行にする。

---

## CSharpier 整形

コミット前にリポジトリルートで実行する（CI は整形漏れを検出するだけで自動修正はしない）。

```bash
mise exec -- dotnet csharpier check .    # 整形漏れの確認（何も出なければ OK）
mise exec -- dotnet csharpier format .   # 整形を適用
```

---

## PR の説明

- テンプレート（[`.github/pull_request_template.md`](../.github/pull_request_template.md)）の項目を埋める。
- 何を・なぜ変えたかが伝わる程度に**短く**書く。
