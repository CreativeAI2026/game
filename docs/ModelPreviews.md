# キャラクター・武器の3Dプレビュー

実装ブランチ: feature/character-weapon-3d-preview

## 連携と設定

CharacterPanel.prefab の CharacterView に CharacterPreviewController を設定済み。ModelArea 内の Character3DPreview と既存 WeaponPreview 内の Weapon3DPreview を RawImage と ModelPreview に接続している。既存の領域サイズ・アンカー・武器ボタン・ステータス参照は維持し、旧モデル説明と2Dプレビュー画像だけを無効化している。

設定を再作成する場合は Unity の Tools > CreativeAI > UI > Setup 3D Model Previews を実行する。保存済みPrefabを使う通常の起動時には実行不要。

PlayerRegistry.Changed、WeaponManager.OnOwnedCountChanged / OnWeaponSwitched、CharacterWeaponSelector.SelectionChanged を購読する。武器所持の情報源は既存の WeaponManager であり、一般アイテム用 InventoryService に武器の別状態を持たせない。

キャラクターは現在の PlayerModel の外見を描画専用リグへコピーし、HumanPoseHandlerでプレビュー専用の立ち姿に整える。骨を複製してから膝・肘を伸ばすため、実プレイヤーの姿勢やTransformは変更しない。外見の更新を取り込むため表示中は0.25秒間隔でも同期する。装備・所持変更では次の描画フレームで同期する。未装備・所持0では全武器を除外する。所持0は詳細と下部UIを非表示にし、入手イベントで自動復帰する。実プレイヤーが武器を装備している間は、下部で閲覧している武器をキャラクタープレビューにも表示する。複製した手のソケットに取り付け、実装備は変更しない。未装備なら閲覧中でもキャラクターは素手を維持する。WeaponManager.Unequip() で所持を維持した素手状態を指定できる。

## 描画と寿命

専用カメラ、768×768のRenderTexture、RawImage、描画専用Transform階層を用いる。MeshRendererは共有メッシュ・マテリアルを参照し、SkinnedMeshRendererはプレビュー専用の複製骨へ接続し、外見・マテリアル・ブレンドシェイプを同期したうえで、スケール補正付きBakeMeshで表示用メッシュへ変換する。ゲーム用PrefabやMonoBehaviour、Animator、Collider、Rigidbodyは複製しない。武器の非均一スケールを保つため、元のTransform階層を描画用に再現する。

キャラクターは左ドラッグでY軸回転。武器はTime.unscaledDeltaTimeを使い20度/秒で回転する。回転中心は描画Boundsの中心に合わせ、Bounds全体を含む球と表示領域の縦横比からカメラを調整する。

カメラは常時無効。URPの明示的なSingleCameraRequest中だけ専用レイヤー31の描画と照明を有効にし、finallyで無効化する。通常カメラの描画へ混入させない。ステージは常駐UIに合わせてシーン遷移を跨ぎ、UIを閉じるとステージ・ベイクメッシュを破棄してRenderTextureをReleaseする。共有メッシュ・マテリアルは破棄しない。購読も解除し、再表示時に現在のプレイヤーへ接続し直す。

## 既存アセットの補修と制限

プレイヤーの起動時に例外になっていた鎌の_heldModel参照を実モデルに接続した。弓の欠落した弦Prefabへの参照は独立したLineRendererとBowString.matで補い、PlayerRig側で弦参照をnullにしていたオーバーライドを除いた。

弓本体の参照先3Dモデルはリポジトリに存在しない。ユーザー指定により、詳細には「3Dモデル未設定」を表示する。刀・鎌・ハンマーは既存の実モデルを表示する。

## 検証（2026-10-21、Unity 6000.4.5f1 / Windows Editor）

- 実Prefabを使用するModelPreviewPlayModeTests: 成功。
- 所持0、取得、未装備、装備変更、選択即時切替、3武器の描画、20度/秒回転、左ドラッグ、右ドラッグ無効、プレイヤーTransform不変を確認。
- EventSystemのレイキャストでプレビューが入力先になること、実マウスのドラッグでYawが変化することをPlay Modeで確認。
- 弓の未設定表示、UI閉鎖時のRenderTexture解放、再表示、常駐UIと同じシーンでのリソース管理を確認。
- WeaponManagerTests / CharacterUITests / WeaponTabViewControllerTests: 27件成功。
- 出力画像を目視確認し、スキニング補正による部位配置と武器の表示を確認。

テストはWindow > General > Test Runnerから実行できる。PlayMode側の対象はModelPreviewPlayModeTests。テスト出力はTemp/ModelPreviewEvidence/。同フォルダのPlayModeResults.xml / EditModeResults.xmlに実行結果を保存した。

実マウスの再確認時だけ、テスト実行前に空のTemp/preview-interactive.requestを作ると装備表示で50秒待機する。その間にGameビューのキャラクターを左ドラッグすると、テストがYawの変化を検証する。

未検証: スタンドアロンビルド・他プラットフォームの実機、実際のフィールドシーン遷移を操作しながらの確認、将来追加される外見差し替えシステム。

## UI_Sandboxの直接再生と所持状態の切替

UI_SandboxのFieldDevBootstrapでプレイヤー生成がOFFになっており、通常の直接再生ではPlayerRegistryが空のままだった。シーンの生成設定をONにし、既存のMain Cameraを再利用する。既にUIとインベントリだけが生成されている場合にも、プレイヤー不在なら生成を続行する。

UnityメニューのTools > CreativeAI > UI > Preview Test Controlsでテスト用ウィンドウを開く。Play Mode中は「武器なし（所持0・素手）」「全部あり（4種所持・刀を装備）」「所持を維持して素手にする」を即時切替できる。変更は再生中のWeaponManagerだけに反映し、セーブファイルやdev_state.jsonは書き換えない。現在のdev_state.jsonは武器0なので、直接再生の初期状態は素手になる。

回帰テストはUI_SandboxをEditorの実再生APIで読み込み、テスト側からのプレイヤー生成を行わない。プレイヤー自動生成、Main Cameraが1台であること、上記メニュー経由の所持0・全所持・復帰も確認する。

UI_Sandboxの確認用プレイヤーにはPreviewOnlyPlayerを付け、通常カメラには常に表示しない。確認用の操作・CharacterControllerを無効にし、パネルを閉じてもワールドにキャラクターが残らない。フィールドの実プレイヤーにはこの設定を適用しない。パネル終了時には立ち姿用リグも破棄する。

立ち姿の回帰テストでは武器選択後も膝の曲がりが2度未満であること、プレビューに選択武器IDが反映されること、実装備が変化しないこと、UI外の確認用モデルが描画されないことを確認する。

## ブレードの待機モーション

ブレード（1001）・大鎌（1003）・ハンマー（1004）はPlayer.fbx内のArmature|Idle_katanaを共通アイドルとしてプレビュー専用Animatorと手動更新PlayableGraphでループ再生する。Time.unscaledDeltaTimeで進め、ゲームの停止中も動く。ゲーム側Animatorや装備は変更しない。イベントとRoot Motionは無効。武器切替時にはグラフを破棄して再構築する。弓・素手へ切替時は保存した立ち姿のTransformへ戻す。パネル終了でもグラフを破棄する。PrefabのWeapon IdleとMelee Attack参照は設定済み。

Play Mode回帰テスト成功（Temp/ModelPreviewEvidence/BladeIdleResults.xml）。再生時間だけでなくボーン回転の変化、他武器での直立、所有0・再取得・閉じて再表示を確認。Idle_katanaは刀を下げた控えめな待機姿勢。

## 共通アイドルとクリック攻撃（feature/preview-weapon-animations）

キャラクタープレビューを左クリックすると通常近接攻撃（Armature_Slash_01_katana）を1回再生し、共通アイドルへ戻る。鎌・ハンマーもゲーム本体の通常攻撃と同じ共通クリップを使用。入り0.1秒・戻り0.15秒のブレンドを適用する。ドラッグは回転だけとし、ドラッグ後のクリック・右クリック・攻撃中の連打は無視する。弓・素手は攻撃しない。武器切替・所持0・パネル終了で攻撃を破棄する。時間停止中もunscaledDeltaTimeで進む。

ゲーム本体のAnimator・攻撃スクリプト・AnimationEvent・ヒット判定・Root Motionは使わず、プレビュー内の骨だけを動かす。

Play Mode回帰テスト成功（Temp/ModelPreviewEvidence/AttackPreviewResults.xml）。3武器のアイドルと攻撃のボーン変化、ドラッグ後の攻撃抑止、右クリック無効、攻撃終了時の復帰、時間停止中の復帰、武器切替・パネル終了による中断を確認。
