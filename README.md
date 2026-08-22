# CreativeAI — Unity プロジェクト

Unity Editor **6000.4.5f1**（Unity 6）で開いてください。バージョンは `ProjectSettings/ProjectVersion.txt` で管理されています。

## はじめに（開発に参加する人へ）

- 環境構築の手順 → [`docs/EnvironmentSetup.md`](docs/EnvironmentSetup.md)
- 開発フロー（ブランチ → コミット → PR → CI） → [`docs/DevelopmentWorkflow.md`](docs/DevelopmentWorkflow.md)

## ディレクトリ構成

```
game/                          ← リポジトリルート
├── Assets/
│   ├── _Project/              本プロジェクトのアセット（基本ここに置く）
│   │   ├── Features/          ゲーム機能。機能ごとにフォルダ + asmdef
│   │   │   ├── Core/          進行度・セーブ・シーン遷移など土台
│   │   │   ├── Player/        プレイヤー
│   │   │   ├── Enemy/         敵
│   │   │   ├── Combat/        戦闘
│   │   │   ├── Crafting/      調合
│   │   │   ├── Inventory/     インベントリ
│   │   │   ├── Scenario/      イベント・会話
│   │   │   ├── Field/         フィールド・マップ
│   │   │   ├── Camera/        カメラ
│   │   │   ├── Audio/         オーディオ再生
│   │   │   └── UI/            UI
│   │   ├── Art/               Models / Textures / Materials / Animations / Shaders / VFX / UI
│   │   ├── Audio/             BGM・SE のファイル
│   │   ├── Scenes/            シーン（Title / Field / Battle / UI プレビュー）
│   │   ├── Settings/          URP / Input System 等の設定
│   │   ├── Resources/         実行時ロードするアセット（ItemDB 等のカタログ）
│   │   ├── Editor/            Editor 拡張・セットアップツール（Tools メニュー）
│   │   └── Tests/             EditMode / PlayMode テスト
│   ├── Plugins/               外部アセット（DOTween 等）
│   └── Resources/             Unity 既定の Resources
├── Packages/                  Unity Package Manager の管理
│   └── manifest.json          依存パッケージ一覧
├── ProjectSettings/           プロジェクト固有の設定（バージョン・物理設定等）
├── docs/                      開発ドキュメント（環境構築・開発フロー）
├── .github/                   CI（workflows）と PR テンプレート・CI 用スクリプト
├── .config/                   dotnet ツール（CSharpier）のバージョン固定
├── .vscode/                   VS Code 設定（共有）
├── mise.toml                  .NET SDK のバージョン固定
├── .editorconfig              コードスタイル
├── .gitignore                 Git 除外ルール
├── .gitattributes             改行コード等の Git 設定
├── README.md                  ← このファイル
├── Library/                   ★ 自動生成（commit しない）
├── Temp/                      ★ 一時ファイル（commit しない）
├── Logs/                      ★ ログ（commit しない）
├── UserSettings/              ★ ユーザー個人設定（commit しない）
└── *.csproj / *.slnx          ★ Unity が自動生成（commit しない）
```

★ は `.gitignore` で除外されています。

## ドキュメント

| ファイル | 内容 |
|----------|------|
| [`docs/EnvironmentSetup.md`](docs/EnvironmentSetup.md) | 環境構築手順（Unity / mise / CSharpier） |
| [`docs/DevelopmentWorkflow.md`](docs/DevelopmentWorkflow.md) | 開発フロー（ブランチ → コミット → PR → CI） |
