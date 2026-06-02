# 環境構築手順書

このリポジトリ（`CreativeAI2026/game`）をローカルで開発できる状態にするまでの手順。
初めてセットアップする人はこの順番で進めれば OK。

- Unity Editor: **6000.4.5f1**（Unity 6）— `ProjectSettings/ProjectVersion.txt` で管理
- C# フォーマッタ: **CSharpier 1.2.6**（`.config/dotnet-tools.json` で固定）
- .NET SDK: **10.0.300**（`mise.toml` で固定）

---

## 0. 前提ツール

| ツール | 用途 | 入手先 |
|--------|------|--------|
| Git | バージョン管理 | https://git-scm.com/ |
| Unity Hub | Unity Editor の管理・起動 | https://unity.com/download |
| mise | .NET SDK のバージョン固定（CSharpier 実行用） | https://mise.jdx.dev/ |
| GitHub CLI（任意） | `gh` で PR 作成を楽にする | https://cli.github.com/ |

> mise を使わず手元の .NET SDK で動かすこともできるが、バージョン差で整形結果がブレる原因になる。チームで揃えるため mise 経由を推奨。

### mise のインストール

入っていない場合は OS に合わせて入れる。

**macOS（Homebrew）**

```bash
brew install mise
```

**Windows**

```powershell
winget install jdx.mise
```

インストール後、シェルに mise を有効化する（[公式手順](https://mise.jdx.dev/getting-started.html)）。

```bash
# zsh（macOS デフォルト）
echo 'eval "$(mise activate zsh)"' >> ~/.zshrc
```

```powershell
# Windows PowerShell
echo 'mise activate pwsh | Out-String | Invoke-Expression' >> $PROFILE
```

新しいシェルを開き直し、`mise --version` で確認する。

---

## 1. リポジトリをクローン

```bash
git clone git@github.com:CreativeAI2026/game.git
cd game
```

---

## 2. Unity Editor をインストール

1. Unity Hub を開く。
2. **Installs → Install Editor** で **6000.4.5f1** を選択してインストール。
   - Hub に出てこない場合は [Unity download archive](https://unity.com/releases/editor/archive) から該当バージョンを入れる。
3. **Projects → Add** でクローンした `game/` フォルダを選択。
4. プロジェクトを開く。初回はインポートに数分かかる（`Library/` が生成される）。

> `Library/` `Temp/` `Logs/` `UserSettings/` は `.gitignore` で除外されている自動生成物。コミットしない。

---

## 3. .NET SDK（mise）をセットアップ

CSharpier をローカルで動かすために .NET SDK を入れる。`mise.toml` でバージョンが固定されている。

```bash
# 初回のみ：mise.toml に書かれた dotnet 10.0.300 を取得
mise install

# CSharpier（dotnet ツール）を取得
mise exec -- dotnet tool restore
```

動作確認:

```bash
mise exec -- dotnet csharpier --version   # 1.2.6 が出れば OK
```

---

## 4. IDE の CSharpier 連携（推奨：保存時に自動整形）

CI は整形漏れを **赤くするだけ**で自動修正はしない（[DevelopmentWorkflow.md](./DevelopmentWorkflow.md) 参照）。
保存時に自動整形しておくと CI で落ちにくく、手戻りが減る。

### VS Code

1. 拡張機能 **CSharpier**（`csharpier.csharpier-vscode`）をインストール。
2. `.vscode/` にワークスペース設定が入っているのでそのまま使う。format-on-save が有効になる。

---

## 5. 動作確認

- Unity Editor でシーンを開いて Play できる。
- 下記が成功する（整形漏れがなければ何も出力されない）:

```bash
mise exec -- dotnet csharpier check .
```

ここまで通れば環境構築は完了。日々の開発の進め方は [DevelopmentWorkflow.md](./DevelopmentWorkflow.md) を参照。

---

## 関連ドキュメント

- [DevelopmentWorkflow.md](./DevelopmentWorkflow.md) — ブランチ → コミット → PR の開発フロー
- `documents/CICD.md` — CI/CD の構成と設計方針（Unity ライセンス登録手順など）
- `documents/DirectoryStructure.md` — `Assets/_Project/` のフォルダ設計
- [../README.md](../README.md) — プロジェクト概要
