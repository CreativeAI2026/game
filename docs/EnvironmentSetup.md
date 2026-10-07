# 環境構築手順書

- Unity Editor: **6000.4.5f1**（Unity 6）— `ProjectSettings/ProjectVersion.txt` で管理
- C# フォーマッタ: **CSharpier 1.2.6**（`.config/dotnet-tools.json` で固定）
- .NET SDK: **10.0.300**（`mise.toml` で固定）

---

## 前提ツール

| ツール | 用途 | 入手先 |
|--------|------|--------|
| Git | バージョン管理 | https://git-scm.com/ |
| Unity Hub | Unity Editor の管理・起動 | https://unity.com/download |
| mise | .NET SDK のバージョン固定（CSharpier 実行用） | https://mise.jdx.dev/ |

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

## リポジトリをクローン

```bash
git clone git@github.com:CreativeAI2026/game.git
cd game
```

---

## Unity Editor をインストール

1. Unity Hub を開く。
2. **Installs → Install Editor** で **6000.4.5f1** を選択してインストール。
3. **Projects → Add** でクローンした `game/` フォルダを選択。
4. プロジェクトを開く。

> `Library/` `Temp/` `Logs/` `UserSettings/` は `.gitignore` で除外されている自動生成物。コミットしない。

---

## .NET SDK（mise）をセットアップ

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

## 動作確認

- Unity Editor でシーンを開いて Play できる。
- 下記が成功する（整形漏れがなければ何も出力されない）:

```bash
mise exec -- dotnet csharpier check .
```