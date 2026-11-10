# 環境構築手順書

- Unity Editor: **6000.4.5f1**（Unity 6）— `ProjectSettings/ProjectVersion.txt` で管理
- C# フォーマッタ: **CSharpier 1.2.6**（`.config/dotnet-tools.json` で固定）
- .NET SDK: **10.0.300**（`mise.toml` で固定）

---

## リポジトリをクローン

Git と SSH 鍵の設定がまだなら先に [GitSetup.md](GitSetup.md) の手順で行う。

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

mise が入っていない場合は先に [MiseSetup.md](MiseSetup.md) の手順で入れる。

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