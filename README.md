# CreativeAI — Unity プロジェクト

Unity Editor **6000.4.5f1**（Unity 6）で開いてください。

## はじめに（開発に参加する人へ）

- 環境構築の手順 → [`docs/EnvironmentSetup.md`](docs/EnvironmentSetup.md)
- 開発フロー（ブランチ名・コミットメッセージ・整形・PR） → [`docs/DevelopmentWorkflow.md`](docs/DevelopmentWorkflow.md)
- イベントのフラグと合成（`events.json`） → [`docs/EventFlags.md`](docs/EventFlags.md)

## ディレクトリ構成

```
game/                          ← リポジトリルート
├── Assets/
│   ├── _Project/              本プロジェクトのアセット（基本ここに置く）
│   │   ├── Features/          ゲーム機能。機能ごとのフォルダにロジック・データを置く
│   │   │   ├── Core/          進行度・ゲームモード・シーン遷移など土台
│   │   │   ├── Event/         会話イベントの定義・再生・マップピン
│   │   │   ├── Player/        プレイヤー・武器
│   │   │   ├── Enemy/         敵・ボス
│   │   │   ├── Combat/        戦闘（当たり判定・パラメータ）
│   │   │   ├── Crafting/      調合（レシピ・調合処理）
│   │   │   ├── Inventory/     アイテム定義・所持品・装備品の能力値
│   │   │   ├── SaveSystem/    セーブ・ロード
│   │   │   ├── Field/         フィールド（扉・拾えるアイテム・シーンの重ね読み）
│   │   │   ├── Camera/        カメラ
│   │   │   ├── Audio/         ゲーム側の効果音イベント
│   │   │   ├── Display/       ウィンドウ表示の切り替え
│   │   │   └── UI/            各機能の画面
│   │   │       ├── Root/          UIRoot・画面切り替え・起動時の常駐生成（GameSession）
│   │   │       ├── Common/        画面共通の部品（スロット・タブ・アニメーション）
│   │   │       ├── HUD/           フィールド中の表示（HP・武器・操作表示・即時食材・暗転）
│   │   │       ├── TitleUI/       タイトル
│   │   │       ├── InventoryUI/   インベントリ
│   │   │       ├── CraftingUI/    調合
│   │   │       ├── CharacterUI/   キャラクター（装備・武器・即時食材）
│   │   │       ├── ConversationUI/ 会話
│   │   │       └── SaveDialog/    セーブ確認
│   │   ├── Modules/           プロジェクトに依存しない仕組み（SoundKit：音の再生・音量設定）
│   │   ├── Art/               Models / Textures / Materials / Animations / Shaders / VFX / UI
│   │   ├── Audio/             SE・SoundCue・AudioMixer
│   │   ├── Scenes/            シーン（Title / Field / Battle / UI プレビュー）
│   │   ├── Settings/          URP / Input System 等の設定
│   │   ├── Resources/         実行時ロードするアセット（ItemCatalog・常駐生成の設定・UI 音・マップピン）
│   │   ├── Editor/            Editor 拡張（Tools メニュー・UI バリデータ・アイテム同期・イベント取り込み・マップ配置・アート生成・Dev State ウィンドウ・会話スキップ）
│   │   └── Tests/             EditMode / PlayMode テストと共通ヘルパー（Shared）
│   ├── Plugins/               外部アセット（DOTween・Unity 用アナライザ）
│   ├── Resources/             DOTween の設定（DOTween がこの場所を前提にしているので動かさない）
│   ├── UI Toolkit/            TextMesh Pro の既定フォント等
│   └── csc.rsp / Default.ruleset  コンパイラ設定（警告をエラー扱い・アナライザのルール）
├── Packages/                  Unity Package Manager の管理
│   └── manifest.json          依存パッケージ一覧
├── ProjectSettings/           プロジェクト固有の設定（バージョン・物理設定等）
├── docs/                      開発ドキュメント（環境構築・mise・開発フロー）
├── .github/                   CI（workflows）と PR テンプレート・CI 用スクリプト
├── .config/                   dotnet ツール（CSharpier）のバージョン固定
├── .vscode/                   VS Code 設定（共有）
├── mise.toml                  .NET SDK のバージョン固定
├── .editorconfig              コードスタイル
├── .csharpierignore           CSharpier の整形対象外
├── .vsconfig                  Visual Studio の必要コンポーネント
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
