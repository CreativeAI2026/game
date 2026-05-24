# CreativeAI — Unity プロジェクト

## 必要環境

| ソフト | バージョン | 入手元 |
|---|---|---|
| **Unity Editor** | **6000.4.5f1**（Unity 6） | [Unity Hub](https://unity.com/download) からインストール |
| **Unity Hub** | 最新 | 同上 |
| **VS Code** | 最新 | [https://code.visualstudio.com/](https://code.visualstudio.com/) |
| **Git** | 任意 | [git-scm.com](https://git-scm.com/) からインストール |

Unity のバージョンは `ProjectSettings/ProjectVersion.txt` で管理されています。**必ず同じバージョン**を Unity Hub からインストールしてください（マイナーバージョン違いだとプロジェクトが破損することがあります）。

---

## 初回セットアップ

### 1. リポジトリを取得

```bash
git clone https://github.com/CreativeAI2026/game.git
cd game
```

### 2. Unity Hub にプロジェクトを登録

1. Unity Hub を起動
2. `Projects` タブ → `Add` → クローンした `game/` フォルダを選択
3. リスト上で Unity バージョンが **6000.4.5f1** になっていることを確認
4. プロジェクトをクリックして開く

### 3. 初回起動の待ち時間に注意

初回は **`Library/` ディレクトリの再生成** が走るため、5〜15 分ほどかかります（PC スペック・パッケージ数による）。途中で中断しないこと。

完了後、`Assets/_Project/Scenes/` 以下の任意のシーンを開いて Play できれば成功です（Phase 1 で `00_Boot.unity` `01_Title.unity` を作成予定。それまでは中身が空でも構いません）。

### 4. VS Code を「External Script Editor」に設定

Unity 内で：

`Unity` メニュー → `Settings` → `External Tools` → `External Script Editor` で **Visual Studio Code** を選択。

これで、Unity 上でスクリプトをダブルクリックすると VS Code が自動で開くようになります。

---

## VS Code セットアップ

### 推奨拡張機能

`.vscode/extensions.json` に推奨拡張が登録されています。VS Code でこのプロジェクトを初めて開くと「推奨拡張をインストールしますか？」とプロンプトが出るので、`Install` を選んでください。

**必須:**

| 拡張機能 | ID | 役割 |
|---|---|---|
| **Unity** | `visualstudiotoolsforunity.vstuc` | Unity 連携・補完・デバッガ |
| **C#** | `ms-dotnettools.csharp` | C# 言語サーバー（Unity 拡張の依存） |

Unity 拡張をインストールすると、依存関係で C# Dev Kit（`ms-dotnettools.csdevkit`）も一緒に入りますが、**Unity 開発では使いません**（後述「よくあるトラブル」参照）。

### 開くフォルダについて

VS Code は **クローンした `game/` フォルダをそのまま開いて**ください。他のディレクトリと組み合わせたマルチルートワークスペースで開くと、C# Dev Kit が誤検出して警告を出すことがあります（→「よくあるトラブル」参照）。

---

## フォルダ構成

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

### 主要パッケージ（`Packages/manifest.json`）

このプロジェクトで使用している主な Unity パッケージ：

| パッケージ | 用途 |
|---|---|
| `com.unity.render-pipelines.universal` | URP（Universal Render Pipeline） |
| `com.unity.inputsystem` | 新 Input System |
| `com.unity.ai.inference` | Unity AI Inference（オンデバイス推論） |
| `com.unity.ai.navigation` | NavMesh（AI ナビゲーション） |
| `com.unity.ai.assistant` | Unity AI アシスタント |
| `com.unity.visualscripting` | Visual Scripting |
| `com.unity.timeline` | Timeline |
| `com.unity.test-framework` | テストフレームワーク |

パッケージの追加・削除は Unity の `Window → Package Manager` から行ってください。`manifest.json` を直接編集する場合はバージョン記述に注意。

---

## Git 運用

### コミット時の注意

**Unity 特有の落とし穴があります。**

- **`.meta` ファイルは必ずコミットする**
  Unity は全アセットに `.meta` を付与し、ファイル間の参照に使います。`.meta` を commit し忘れると **他メンバーの環境で参照が壊れます**。
- **`.cs` を追加・削除したときは Unity Editor を一度起動してからコミット**
  Editor 起動時に `Library/` のメタ情報が更新されるため。
- **シーン（`.unity`）・Prefab（`.prefab`）の同時編集は競合の温床**
  作業前にチーム内で「誰がどのシーンを触っているか」を共有してください。

### `.gitignore` で除外しているもの

- `Library/`, `Temp/`, `Logs/`, `UserSettings/` — 自動生成・個人設定
- `*.csproj`, `*.sln`, `*.slnx` — VS Code が自動生成
- `Build/`, `Builds/` — ビルド成果物
- `.DS_Store`, `.vs/` — OS / IDE 固有

詳細は `.gitignore` 参照。

---

## ビルド

`File → Build Settings → Build`（または `Build And Run`）から実行します。
プラットフォーム別の詳細手順は別途整備予定。

---

## よくあるトラブル

### 1. VS Code に `Failed to restore NuGet packages for the solution.` が出る

**無視して OK。Unity 開発に影響しません。**

C# Dev Kit 拡張機能が Unity プロジェクトを通常の .NET プロジェクトと誤認し、不要な NuGet 復元を試みて失敗するために出るエラーです。Unity Editor のビルド・実行・VS Code の IntelliSense は正常に動きます。

気になる場合は、拡張機能パネル（`Ctrl+Shift+X` / macOS: `Cmd+Shift+X`） → `C# Dev Kit` → 歯車 → `Disable (Workspace)` で無効化できます。

### 2. Unity でプロジェクトを開いたら大量のエラーが出る

Unity バージョンが違う可能性があります。Unity Hub でプロジェクトに表示されているバージョンが **6000.4.5f1** か確認してください。

### 3. VS Code で IntelliSense が効かない

- Unity 拡張機能（`visualstudiotoolsforunity.vstuc`）がインストールされているか確認
- Unity Editor を一度起動して `.csproj` を再生成させる
- コマンドパレット（`Ctrl+Shift+P` / macOS: `Cmd+Shift+P`） → `Developer: Reload Window` で VS Code をリロード

### 4. シーンを開いたら Pink（マゼンタ）になる

URP のマテリアルが Standard Shader で作られている等。`Edit → Rendering → Materials → Convert All Built-in Materials to URP` を試してください。

### 5. `Library/` を消してしまった / 壊れた

問題ありません。Unity を再起動すれば自動再生成されます（時間はかかります）。
