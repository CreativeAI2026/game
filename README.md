# CreativeAI — Unity プロジェクト

Unity Editor **6000.4.5f1**（Unity 6）で開いてください。

## はじめに（開発に参加する人へ）

- 環境構築の手順 → [`docs/EnvironmentSetup.md`](docs/EnvironmentSetup.md)
- 開発フロー（ブランチ名・コミットメッセージ・整形・PR） → [`docs/DevelopmentWorkflow.md`](docs/DevelopmentWorkflow.md)

## ディレクトリ構成

```
game/                          ← リポジトリルート
├── Assets/
│   ├── _Project/              本プロジェクトのアセット（基本ここに置く）
│   │   ├── Features/          ゲーム機能。機能ごとに Scripts / Prefabs / Data（設定アセット・JSON）を置く
│   │   │   ├── GameState/     進行度・ゲームモード・メニューが開いているか
│   │   │   ├── SceneFlow/     シーン遷移・到着位置・シーンの重ね読み
│   │   │   ├── UiRequests/    ゲーム側から UI への依頼（会話・暗転・調合画面・操作表示）
│   │   │   ├── Event/         会話イベントの定義・再生、他機能との窓口。Data/events.json が台本
│   │   │   ├── MapPin/        マップピン（目的地の小さいマーク）
│   │   │   ├── Player/        プレイヤー・武器・焦り度の判定（Panic）
│   │   │   ├── Enemy/         敵・ボス（TutorialBoss / KurageBoss / HakanageBoss）
│   │   │   ├── Combat/        当たり判定・ダメージ計算・各キャラの能力値
│   │   │   ├── Items/         アイテムの定義・一覧（ItemCatalog）・能力値と抽選。Data/items.json が元データ
│   │   │   ├── Inventory/     所持品（持ち物の出し入れ・即時食材の枠）
│   │   │   ├── Crafting/      調合（レシピ・調合処理・合成装置）
│   │   │   ├── Field/         扉・調べる・拾えるアイテムと拾い済みの記録
│   │   │   ├── SaveSystem/    セーブ・ロード
│   │   │   ├── Camera/        カメラリグ・ズーム・揺れ
│   │   │   ├── Audio/         プレイヤーの立てた物音を敵が聞きつける仕組み
│   │   │   ├── Sound/         音を鳴らす部品（効果音・BGM・UI 音・音量設定）。使い方は Sound/README.md
│   │   │   ├── Effects/       エフェクトの使い回し（ObjectPool）
│   │   │   ├── Session/       ゲームの開始（タイトルから）と常駐一式の生成。UI より上の独自のアセンブリ
│   │   │   ├── UI/            各機能の画面
│   │   │   │   ├── Root/          UIRoot・画面切り替え・EventSystem
│   │   │   │   ├── Slots/         アイテムを入れるスロットの部品
│   │   │   │   ├── Tabs/          タブの部品
│   │   │   │   ├── ItemDisplay/   アイテムの能力値・調べるアイコンの表示
│   │   │   │   ├── Motion/        スライド・拡大・文字送りなどの演出
│   │   │   │   ├── Common/        上に入らない共通の部品（パネル・テーマ・補助）
│   │   │   │   ├── HUD/           フィールド中の表示（HP・武器・操作表示・即時食材・暗転・ピンの矢印）
│   │   │   │   ├── Title/         タイトル
│   │   │   │   ├── Inventory/     インベントリ
│   │   │   │   ├── Crafting/      調合
│   │   │   │   ├── Character/     キャラクター（装備・武器・即時食材）
│   │   │   │   ├── Conversation/  会話
│   │   │   │   ├── Map/           ミニマップ・全画面マップ
│   │   │   │   └── SaveDialog/    セーブ確認
│   │   │   └── Playtest/      試遊会用の舞台（独自のアセンブリ）
│   │   ├── DevTools/          開発でだけ使うもの（フィールドを直接 Play したときの起動・Data/dev_state.json・UI 確認シーンの動かし役・デバッグ表示）。一番上の独自のアセンブリ
│   │   ├── Art/               素材を種類ごとに（Models / Textures / Materials / Animations / Shaders / VFX / UI）。VFX は見た目だけのエフェクト。当たり判定・取得などゲームのルールを持つ物は各機能の Prefabs/ へ
│   │   ├── Audio/             SE・SoundCue・AudioMixer
│   │   ├── Scenes/            シーン（Title / Field / Battle / UI プレビュー）
│   │   ├── Settings/          URP（Renderer・全体設定・既定の Volume）/ Input System の設定
│   │   ├── Resources/         実行時ロードするアセット（ItemCatalog・常駐生成の設定・UI 音・マップピンの見た目）
│   │   ├── Editor/            Editor 拡張（Tools メニュー）。CiBuild.cs は CI のビルドの入口
│   │   │   ├── Event/         events.json の取り込み
│   │   │   ├── DevTools/      Dev State ウィンドウ（好きなイベントの直前から Play）
│   │   │   ├── Items/         アイテムの同期・アイコンの取り込み設定と透過チェック
│   │   │   ├── Map/           マップ生成・NavMesh / オクルージョン / ミニマップの焼き込み・小物シーン・アイテム配置
│   │   │   ├── Player/        弓の構えの調整・射程の編集・武器データの Inspector
│   │   │   ├── Panic/         焦り度ログの集計
│   │   │   ├── Sound/         音素材の音量そろえ・音声エディタ（ffmpeg）
│   │   │   ├── Art/           エフェクトの生成・アニメーションの書き出し・取り込み時の処理
│   │   │   ├── UI/            HUD の確認用 Inspector・プレビュー操作・フォントの掃除・会話スキップ
│   │   │   └── Playtest/      ボス体験用ビルド
│   │   └── Tests/             EditMode / PlayMode テストと共通ヘルパー（Shared）。EditMode は確かめるクラスと同じ構成のフォルダに置く（例: Features/UI/HUD のテストは EditMode/Features/UI/HUD。Editor/ のテストは Unity の特別なフォルダ名を避けて EditMode/EditorTools/）
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

アセンブリはフォルダで決まる: `Features/UI` の下は UI、`Session`・`Playtest`・`DevTools` はそれぞれ独自、それ以外の `Features` は Gameplay。
使える向きは DevTools → Session → UI → Gameplay の一方通行（本番のコードは DevTools を使わない）。Gameplay から UI に頼むときは `UiRequests` の窓口を通す。
