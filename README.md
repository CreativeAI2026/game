# CreativeAI — Unity プロジェクト

Unity Editor **6000.4.5f1**（Unity 6）で開いてください。バージョンは `ProjectSettings/ProjectVersion.txt` で管理されています。

## クローン

```bash
git clone https://github.com/CreativeAI2026/game.git
```

Unity Hub の `Projects` → `Add` でクローンした `game/` フォルダを選択して開きます。

## ディレクトリ構成

```
game/                        ← リポジトリルート
├── Assets/                  ゲーム本体のアセット
│   └── _Project/            本プロジェクトのアセット（詳細は documents/DirectoryStructure.md）
│       ├── Features/        ゲーム機能（Player・Enemy・UI 等）
│       ├── Art/             3D モデル・テクスチャ・アニメ・VFX 等
│       ├── Audio/           BGM・SE
│       ├── Scenes/          シーン
│       ├── Settings/        URP / Input System 等の設定
│       └── Shared/          機能横断の共有資源
├── Packages/                Unity Package Manager の管理
│   └── manifest.json        依存パッケージ一覧
├── ProjectSettings/         プロジェクト固有の設定（バージョン・物理設定等）
├── Library/                 ★ 自動生成（commit しない）
├── Temp/                    ★ 一時ファイル（commit しない）
├── Logs/                    ★ ログ（commit しない）
├── UserSettings/            ★ ユーザー個人設定（commit しない）
├── .vscode/                 VS Code 設定（共有）
├── .gitignore               Git 除外ルール
└── README.md                ← このファイル
```

★ は `.gitignore` で除外されています。`Assets/_Project/` 配下の詳細なフォルダ設計は [`documents/DirectoryStructure.md`](../documents/DirectoryStructure.md) を参照してください。
