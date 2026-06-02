# 開発フロー（ブランチ → コミット → PR）

変更は必ず **ブランチを切って PR 経由**で `main` に入れる。`main` への直 push は禁止（Branch protection）。
CI（format / meta / compile）が緑にならないと merge できない。

環境構築がまだの人は先に [EnvironmentSetup.md](./EnvironmentSetup.md) を済ませること。

---

## 全体の流れ

```
main を最新化 → ブランチを切る → 実装 → CSharpier 整形 → コミット → push → PR 作成 → CI 緑 → merge
```

---

## 1. main を最新化してブランチを切る

```bash
git switch main                # main ブランチに移動
git pull origin main           # リモートの最新を取り込む
git switch -c feature/<内容>   # 作業用ブランチを作って移動（-c = 新規作成）
```

### ブランチ名の付け方

`<種別>/<内容>` の形式。`<内容>` は英小文字 + ハイフン。

| 種別 | 用途 | 例 |
|------|------|-----|
| `feature/` | 機能追加 | `feature/player-move-system` |
| `fix/` | バグ修正 | `fix/enemy-spawn-null` |
| `docs/` | ドキュメント | `docs/dev-onboarding` |
| `chore/` | 設定・雑務 | `chore/update-gitignore` |

---

## 2. 実装する

- 1 ブランチ = 1 つの目的に絞る（レビューしやすく、衝突しにくい）。
- Unity でアセットを追加・移動したら、対応する `.meta` も一緒にコミットする（CI の .meta 整合チェックで弾かれる）。

---

## 3. CSharpier で整形する（コミット前に必須）

CI（`csharpier check`）は **整形漏れを赤くするだけで自動修正はしない**。
修正は手元で `format` を走らせる必要がある。

### 推奨：IDE の保存時自動整形

VS Code の CSharpier 拡張で format-on-save を有効にしておけば、保存のたびに整形される
（設定は [EnvironmentSetup.md](./EnvironmentSetup.md#4-ide-の-csharpier-連携推奨保存時に自動整形) 参照）。
これが一番ラクで、CI で落ちにくい。

### 手動で整形する場合

push 前にリポジトリルートで実行:

```bash
mise exec -- dotnet csharpier check .    # 整形漏れがあるか確認（何も出なければ OK）
mise exec -- dotnet csharpier format .   # 整形を適用
```

整形で差分が出たら、それも含めてコミットする。

---

## 4. コミットする

```bash
git add <変更したファイル>
git commit -m "プレイヤーの移動システムを追加"
```

### コミットメッセージの方針

- **日本語で書く**（このプロジェクトの慣習）。
- 「何をしたか」が分かる簡潔な文にする。例:
  - `プレイヤーの移動システムを追加`
  - `敵スポーン時の null 参照を修正`
  - `CSharpier の整形を適用`
- 整形だけのコミットは内容コミットと分けると履歴が読みやすい（任意）。

---

## 5. push する

```bash
git push -u origin feature/<内容>
```

（2 回目以降の push は `git push` だけで OK。）

---

## 6. PR を作成する

GitHub の画面（ブラウザ）から作成する。

1. push 後、リポジトリ（https://github.com/CreativeAI2026/game）を開く。
   - push 直後なら上部に **「Compare & pull request」** ボタンが出るのでそれを押す。
   - 出ていなければ **Pull requests** タブ → **New pull request** から、`base: main` ← `compare: feature/<内容>` を選ぶ。
2. 説明欄に [`.github/pull_request_template.md`](../.github/pull_request_template.md) のテンプレートが自動で入るので、各項目を埋める。
   - **概要**（何を・なぜ）
   - **変更内容**
   - **動作確認**（Unity Editor で確認した / CI が緑）
   - **関連 Issue**（例: `Closes #123`）
3. **Create pull request** を押す。

> GitHub CLI（`gh`）を使ってもよい。

---

## 7. CI を緑にする

PR を作ると以下が自動で走る:

| チェック | 内容 | 落ちたときの直し方 |
|----------|------|--------------------|
| **format** | CSharpier の整形漏れ | `mise exec -- dotnet csharpier format .` → コミット → push |
| **meta** | `.meta` の欠落・孤児 | 不足 `.meta` を追加 / 孤児 `.meta` を削除してコミット |
| **compile** | 全 asmdef がコンパイル通るか（GameCI） | エラー箇所を修正してコミット |

全部緑になるまで修正を push し続ける（PR は自動で更新される）。

---

## 8. merge

- **今回はレビューなし。CI（format / meta / compile）が緑になったら merge してよい。**
- CI 指摘で落ちたら同じブランチで修正 → push（PR に反映される）。
- merge 後はブランチを削除してよい。

```bash
git switch main
git pull origin main
git branch -d feature/<内容>   # ローカルの後始末
```

---

## 関連ドキュメント

- [EnvironmentSetup.md](./EnvironmentSetup.md) — 環境構築手順
