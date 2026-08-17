#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using CreativeAI.Core;
using CreativeAI.Core.SceneManagement;
using CreativeAI.UI.Common;
using CreativeAI.UI.CharacterUI;
using CreativeAI.UI.InventoryUI;
using CreativeAI.UI.LoadingOverlay;
using CreativeAI.UI.SaveDialog;
using CreativeAI.UI.TitleUI;
using Object = UnityEngine.Object;
using CreativeAI.UI;
using CreativeAI.Gameplay;
using Text = TMPro.TextMeshProUGUI;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 01_Title / Field_Area00(スカフォールド)の2シーンを生成し、Build Settings に登録する(Title 先頭)。
    /// 手作りの本番フィールド は上書きしない。
    /// - アプリ常駐(SceneController + ロードオーバーレイ / EventSystem)は Title が生成する(Boot シーンは廃止)。
    /// - セッション常駐の UI レイヤー(UIRoot)は Title で Prefab 化し、TitleUIController が「はじめる」で生成する。
    ///   HUD(HP) / 右上アイコンバー(HudIconBar) / 即時食材使用UI / 武器切替UI / 各パネル / 会話UI を
    ///   UI ごとに別 Canvas で束ねる。フィールドシーンには UI を置かない(spec/UIImplementation.md)。
    /// Tools > CreativeAI > Setup Initial Scenes から実行。
    /// </summary>
    public static class SetupInitialScenes
    {
        private const string TitlePath = "Assets/_Project/Scenes/01_Title.unity";

        // セッション常駐 UI レイヤー(UIRoot)の Prefab 出力先。TitleUIController から参照して生成する。
        private const string UIRootPrefabPath =
            "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab";

        // 生成器はスカフォールド用の Field_Area00 を作る。手作りの本番フィールド は上書きしない。
        private const string FieldPath = "Assets/_Project/Scenes/Field/Field_Area00.unity";
        private const string TitleBgPath = "Assets/_Project/Art/UI/Backgrounds/bg_title_main.png";
        private const string CharacterBgPath =
            "Assets/_Project/Art/UI/Backgrounds/bg_character_main.png";
        private const string InventoryBgPath =
            "Assets/_Project/Art/UI/Backgrounds/bg_inventory_main.png";
        private const string AppleIconPath =
            "Assets/_Project/Art/UI/Icons/Items/item_food_apple.png";
        private const string ClockIconPath =
            "Assets/_Project/Art/UI/Icons/Items/item_equipment_clock.png";

        [MenuItem("Tools/CreativeAI/Setup Initial Scenes")]
        public static void Run()
        {
            // バッチモード(CLI: -executeMethod)ではダイアログが出せず false 相当になるため、
            // 対話確認をスキップして常に上書き実行する(手動 GUI 実行時のみ確認する)。
            bool anyExists = File.Exists(TitlePath) || File.Exists(FieldPath);
            if (anyExists && !Application.isBatchMode)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Setup Initial Scenes",
                    $"シーンファイルが既に存在します。上書きしますか？\n\n対象:\n- {TitlePath}\n- {FieldPath}",
                    "上書きする",
                    "キャンセル"
                );
                if (!overwrite)
                    return;
            }

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            CreateTitleScene();
            CreateFieldScene();
            RegisterBuildSettings();

            EditorSceneManager.OpenScene(TitlePath);

            const string summary =
                "完了しました。\n\n- 01_Title / Field_Area00(スカフォールド) を生成\n- Title にアプリ常駐(SceneController + ロードオーバーレイ + EventSystem)を配置\n- セッション常駐の UI レイヤー UIRoot を Prefab 化し TitleUIController に配線\n  (HUD / HudIconBar / 即時食材使用UI / 武器切替UI / Character・Inventory・Save パネル / 会話UI)\n- Field_Area00 は 3D 世界のみ(UI は置かない)\n- 手作りの本番フィールド は上書きしていません\n- Build Settings に登録(Title を先頭)\n- 01_Title を開きました\n\nそのまま Play してください。";
            if (Application.isBatchMode)
                Debug.Log("[SetupInitialScenes] " + summary);
            else
                EditorUtility.DisplayDialog("Setup Initial Scenes", summary, "OK");
        }

        // ---------------- アプリ常駐(SceneController + ロードオーバーレイ)----------------
        // Title シーンに置き、起動時に1回だけ生成する(spec §6.1「生成はすべてタイトルが担う」)。
        // Canvas/Overlay は SceneController の子。タイトル再入場時は SceneController.Awake の
        // Instance ガードが重複した PersistentSystems ごと Destroy するので二重生成しない。
        private static void CreatePersistentSystems()
        {
            var systems = new GameObject("PersistentSystems");
            systems.AddComponent<SceneController>();

            var canvasGo = new GameObject("PersistentCanvas");
            canvasGo.transform.SetParent(systems.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            ConfigureScaler(canvasGo.AddComponent<CanvasScaler>());
            canvasGo.AddComponent<GraphicRaycaster>();

            var overlayGo = new GameObject("LoadingOverlay");
            overlayGo.transform.SetParent(canvasGo.transform, false);
            StretchFull(overlayGo.AddComponent<RectTransform>());
            var canvasGroup = overlayGo.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            var overlayImage = overlayGo.AddComponent<Image>();
            overlayImage.color = Color.black;
            overlayImage.raycastTarget = true;

            CreateText(
                overlayGo.transform,
                "LoadingText",
                "Loading...",
                anchorMin: new Vector2(0.5f, 0.14f),
                anchorMax: new Vector2(0.5f, 0.14f),
                size: new Vector2(600, 60),
                fontSize: 36,
                color: new Color(1f, 1f, 1f, 0.85f)
            );

            var slider = CreateProgressBar(overlayGo.transform);

            var overlayController = overlayGo.AddComponent<LoadingOverlayController>();
            SetRef(overlayController, "_canvasGroup", canvasGroup);
            SetRef(overlayController, "_progressBar", slider);
        }

        // ---------------- 01_Title ----------------
        private static void CreateTitleScene()
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single
            );

            // アプリ常駐(SceneController + ロードオーバーレイ)は Title が生成する(Boot 廃止)。
            CreatePersistentSystems();

            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ConfigureScaler(canvasGo.AddComponent<CanvasScaler>());
            canvasGo.AddComponent<GraphicRaycaster>();

            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(canvasGo.transform, false);
            StretchFull(bgGo.AddComponent<RectTransform>());
            var bgImage = bgGo.AddComponent<Image>();
            var bgSprite = LoadSpriteWithImport(TitleBgPath);
            if (bgSprite != null)
            {
                bgImage.sprite = bgSprite;
                bgImage.color = Color.white;
                bgImage.preserveAspect = false;
                bgImage.type = Image.Type.Simple;
            }
            else
            {
                bgImage.color = new Color(0.08f, 0.08f, 0.12f, 1f);
            }
            bgImage.raycastTarget = false;

            var buttonGo = new GameObject("TapToStartButton");
            buttonGo.transform.SetParent(canvasGo.transform, false);
            StretchFull(buttonGo.AddComponent<RectTransform>());
            var btnImage = buttonGo.AddComponent<Image>();
            btnImage.color = new Color(0f, 0f, 0f, 0.01f);
            btnImage.raycastTarget = true;
            var button = buttonGo.AddComponent<Button>();
            button.transition = Selectable.Transition.None;

            CreateText(
                buttonGo.transform,
                "Label",
                "Tap to Start",
                anchorMin: new Vector2(0.5f, 0.3f),
                anchorMax: new Vector2(0.5f, 0.3f),
                size: new Vector2(800, 80),
                fontSize: 42,
                color: new Color(1f, 1f, 1f, 0.85f)
            );

            // 開始処理(プレイヤーリグ生成)。PlayerRig Prefab スロットは未割当のまま
            // (プレイヤー担当が Project の PlayerRig Prefab をドラッグ)。
            var starterGo = new GameObject("GameStarter");
            var starter = starterGo.AddComponent<GameStarter>();

            var titleController = canvasGo.AddComponent<TitleUIController>();
            SetRef(titleController, "_tapToStartButton", button);
            SetRef(titleController, "_gameStarter", starter);
            // 生成器の Title はスカフォールド用 Field_Area00 へ遷移(本番フィールドに依存しない)。
            SetStr(titleController, "_nextSceneName", SceneNames.FieldArea00);

            // セッション常駐の UI レイヤーを Prefab 化して TitleUIController に配線する
            // (「はじめる/続きから」で UIRoot.EnsureResident が Instantiate → DontDestroyOnLoad)。
            var uiRootPrefab = BuildAndSaveUIRootPrefab();
            if (uiRootPrefab != null)
                SetRef(titleController, "_uiRootPrefab", uiRootPrefab);

            // EventSystem はアプリ常駐(Title の1つを DontDestroyOnLoad 化)。フィールドには置かない。
            EnsureInputSystemEventSystem();
            var eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem != null && eventSystem.GetComponent<PersistentEventSystem>() == null)
                eventSystem.gameObject.AddComponent<PersistentEventSystem>();

            EditorSceneManager.SaveScene(scene, TitlePath);
        }

        // ---------------- Field_Area00(スカフォールド)----------------
        // フィールドシーンは 3D 世界のみ(地形・敵・EventTrigger)。UI / EventSystem / 常駐マネージャは置かない
        // ── UI は Title で生成する UIRoot(セッション常駐)へ集約、EventSystem は Title のアプリ常駐を使い回す。
        private static void CreateFieldScene()
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single
            );

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10, 1, 10);
            var camera = Object.FindAnyObjectByType<Camera>();
            if (camera != null)
            {
                camera.transform.position = new Vector3(0, 5, -10);
                camera.transform.rotation = Quaternion.Euler(20, 0, 0);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(FieldPath));
            EditorSceneManager.SaveScene(scene, FieldPath);
        }

        // ---------------- セッション常駐 UI レイヤー(UIRoot Prefab)----------------
        // UI ごとに別 Canvas を持つ整理用の親 GameObject を組み、Prefab として保存して返す
        // (Title の TitleUIController に配線し、「はじめる」で Instantiate → DontDestroyOnLoad)。
        // 重なり順は各 Canvas の sortingOrder で決める(HUD/バー/食材/武器=0、操作パネル=10、会話=20)。
        // 排他パネル(Character/Inventory/Save)は各自の Canvas を常時アクティブにし、中身のパネルを
        // UiRouter が SetActive で出し入れする(パネル未表示時の空 Canvas はほぼ無コスト)。
        private static GameObject BuildAndSaveUIRootPrefab()
        {
            var root = new GameObject("UIRoot");
            root.AddComponent<UIRoot>();
            var router = root.AddComponent<UiRouter>();

            // HP の HUD: 入力を受けないので GraphicRaycaster を持たせない(頻繁な更新での再バッチも避ける)。
            // 中身(HP バー等)は視覚/プレイヤー班の Prefab を後から差し込む。ここでは空 Canvas の骨組みのみ。
            CreateUICanvas(root.transform, "HUD", sortingOrder: 0, addRaycaster: false);

            // 右上アイコンバー(HudIconBar): HP とは別 Canvas。モード連動で自分を出し入れする。
            var iconBarGo = CreateUICanvas(root.transform, "HudIconBar", 0, addRaycaster: true);
            var charBtn = CreateMenuIconButton(
                iconBarGo.transform,
                "CharacterButton",
                -320,
                new Color(0.55f, 0.4f, 0.75f, 1f)
            );
            var invBtn = CreateMenuIconButton(
                iconBarGo.transform,
                "InventoryButton",
                -210,
                new Color(0.4f, 0.65f, 0.45f, 1f)
            );
            var saveBtn = CreateMenuIconButton(
                iconBarGo.transform,
                "SaveButton",
                -100,
                new Color(0.85f, 0.55f, 0.3f, 1f)
            );
            var iconBar = iconBarGo.AddComponent<HudIconBar>();
            SetRef(iconBar, "_router", router);
            SetRef(iconBar, "_characterButton", charBtn);
            SetRef(iconBar, "_inventoryButton", invBtn);
            SetRef(iconBar, "_saveButton", saveBtn);
            SetRef(iconBar, "_canvas", iconBarGo.GetComponent<Canvas>());
            SetRef(iconBar, "_raycaster", iconBarGo.GetComponent<GraphicRaycaster>());

            // 即時食材使用UI / 武器切替UI: 全モード常時表示。中身は他班(gameplay)が後から入れるので
            // ここでは空 Canvas の骨組みのみ(SetActive で出し入れできる状態にしておく)。
            CreateUICanvas(root.transform, "ImmediateFoodUI", 0, addRaycaster: true);
            CreateUICanvas(root.transform, "WeaponSwitchUI", 0, addRaycaster: true);

            // 操作で開くパネル(排他)。各自の Canvas(sortingOrder=10)配下に既存ビルダーで組み、
            // パネル本体を UiRouter に登録する。
            var characterCanvas = CreateUICanvas(
                root.transform,
                "CharacterUI",
                10,
                addRaycaster: true
            );
            var characterPanel = CreateCharacterPanel(characterCanvas.transform);

            var inventoryCanvas = CreateUICanvas(
                root.transform,
                "InventoryUI",
                10,
                addRaycaster: true
            );
            var inventoryPanel = CreateInventoryPanel(inventoryCanvas.transform);

            var saveCanvas = CreateUICanvas(root.transform, "SaveUI", 10, addRaycaster: true);
            var savePanel = CreateSaveDialog(saveCanvas.transform);

            SetRef(router, "_characterUI", characterPanel.gameObject);
            SetRef(router, "_inventoryUI", inventoryPanel.gameObject);
            SetRef(router, "_saveUI", savePanel.gameObject);
            // _craftUI は調合場所実装時に配線(未割当のまま=調合は開かない)。

            // 会話UI(DialogueUI): EventPlayer が出し入れする。中身は会話UI班の Prefab を後から入れる骨組み。
            var dialogue = CreateUICanvas(root.transform, "DialogueUI", 20, addRaycaster: true);
            dialogue.SetActive(false);

            Directory.CreateDirectory(Path.GetDirectoryName(UIRootPrefabPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, UIRootPrefabPath);
            Object.DestroyImmediate(root); // シーンには残さず Prefab のみ(TitleUIController が参照して生成)
            if (prefab == null)
                Debug.LogError(
                    $"[SetupInitialScenes] UIRoot Prefab の保存に失敗: {UIRootPrefabPath}"
                );
            return prefab;
        }

        /// <summary>UIRoot 配下の 1 UI = 1 Canvas を作る。Overlay + スケーラ、必要なら Raycaster。</summary>
        private static GameObject CreateUICanvas(
            Transform parent,
            string name,
            int sortingOrder,
            bool addRaycaster
        )
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            ConfigureScaler(go.AddComponent<CanvasScaler>());
            if (addRaycaster)
                go.AddComponent<GraphicRaycaster>();
            return go;
        }

        // ---------------- Build Settings ----------------
        private static void RegisterBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitlePath, true),
                new EditorBuildSettingsScene(FieldPath, true),
            };
        }

        // ---------------- Helpers ----------------
        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void ConfigureScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string content,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            int fontSize,
            Color color,
            Vector2 anchoredPosition = default
        )
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPosition;
            var text = go.AddComponent<Text>();
            text.text = content;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = fontSize;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta
        )
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPosition;

            var image = go.AddComponent<Image>();
            // 全ボタンを丸/ピル型にする（正方形→円、矩形→楕円ピル）
            var roundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (roundSprite != null)
                image.sprite = roundSprite;
            var baseColor = new Color(0.3f, 0.45f, 0.65f, 1f);
            image.color = baseColor;
            image.raycastTarget = true;

            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = new Color(0.4f, 0.55f, 0.75f, 1f);
            colors.pressedColor = new Color(0.2f, 0.3f, 0.5f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.targetGraphic = image;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            StretchFull(labelGo.AddComponent<RectTransform>());
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = label;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = 24;
            labelText.color = Color.white;
            labelText.raycastTarget = false;

            return button;
        }

        /// <summary>
        /// セーブ確認ダイアログ。フィールドを暗転＋中央にメッセージと はい/いいえ ボタン。
        /// </summary>
        private static UIPanelStub CreateSaveDialog(Transform parent)
        {
            var dialogGo = new GameObject("SaveDialog");
            dialogGo.transform.SetParent(parent, false);
            StretchFull(dialogGo.AddComponent<RectTransform>());

            // 半透明ダーク背景（クリック貫通防止＋フィールド暗転）
            var bg = dialogGo.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.45f);
            bg.raycastTarget = true;

            // 確認メッセージ
            CreateText(
                dialogGo.transform,
                "Message",
                "ここまでの変更をセーブしますか？",
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                size: new Vector2(900, 80),
                fontSize: 36,
                color: Color.white,
                anchoredPosition: new Vector2(0, 40)
            );

            // はい / いいえ（テキストのみ）
            var yesBtn = CreateTextOnlyButton(
                dialogGo.transform,
                "YesButton",
                "はい",
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                anchoredPosition: new Vector2(-100, -60),
                sizeDelta: new Vector2(160, 60),
                fontSize: 30,
                color: new Color(0.95f, 0.8f, 0.4f, 1f)
            );

            var noBtn = CreateTextOnlyButton(
                dialogGo.transform,
                "NoButton",
                "いいえ",
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                anchoredPosition: new Vector2(100, -60),
                sizeDelta: new Vector2(160, 60),
                fontSize: 30,
                color: new Color(0.8f, 0.8f, 0.85f, 1f)
            );

            dialogGo.SetActive(false);

            var dialog = dialogGo.AddComponent<SaveDialogController>();
            SetRef(dialog, "_yesButton", yesBtn);
            SetRef(dialog, "_noButton", noBtn);
            return dialog;
        }

        /// <summary>
        /// インベントリ画面を構築する。
        /// 上部: カテゴリタブ(武器/装備品/食材) / 左: アイテムグリッド / 右: 詳細欄。
        /// タブ切替・アイテム選択ロジックは未実装(スタブ)。
        /// </summary>
        private static UIPanelStub CreateInventoryPanel(Transform parent)
        {
            // ---- InventoryPanel ----
            var panelGo = new GameObject("InventoryPanel");
            panelGo.transform.SetParent(parent, false);
            StretchFull(panelGo.AddComponent<RectTransform>());
            var bg = panelGo.AddComponent<Image>();
            var bgSprite = LoadSpriteWithImport(InventoryBgPath);
            if (bgSprite != null)
            {
                bg.sprite = bgSprite;
                bg.color = Color.white;
                bg.preserveAspect = false;
                bg.type = Image.Type.Simple;
            }
            else
            {
                bg.color = new Color(0.1f, 0.12f, 0.18f, 1f);
            }
            bg.raycastTarget = true;

            // ---- Inventory ----
            var inventoryGo = new GameObject("Inventory");
            inventoryGo.transform.SetParent(panelGo.transform, false);
            var inventoryRt = inventoryGo.AddComponent<RectTransform>();
            inventoryRt.anchorMin = new Vector2(0f, 0f);
            inventoryRt.anchorMax = new Vector2(0.7f, 1f);
            inventoryRt.offsetMin = Vector2.zero;
            inventoryRt.offsetMax = Vector2.zero;

            // ScrollRect
            var scrollRect = inventoryGo.AddComponent<ScrollRect>();
            var scrollImage = inventoryGo.AddComponent<Image>();
            scrollImage.color = new Color(0, 0, 0, 0.001f);

            // Viewport
            var viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(inventoryGo.transform, false);
            var viewportRt = viewportGo.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            viewportGo.AddComponent<RectMask2D>();

            // Content
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);
            var grid = contentGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(130, 130);
            grid.spacing = new Vector2(12, 12);
            grid.padding = new RectOffset(38, 38, 32, 32);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            grid.childAlignment = TextAnchor.UpperCenter;
            contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;

            scrollRect.viewport = viewportRt;
            scrollRect.content = contentRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;

            // Scrollbar Vertical
            var scrollbarGo = new GameObject("Scrollbar Vertical");
            scrollbarGo.transform.SetParent(inventoryGo.transform, false);
            var scrollbarRt = scrollbarGo.AddComponent<RectTransform>();
            scrollbarRt.anchorMin = new Vector2(1f, 0f);
            scrollbarRt.anchorMax = new Vector2(1f, 1f);
            scrollbarRt.offsetMin = new Vector2(-20f, 0f);
            scrollbarRt.offsetMax = Vector2.zero;
            var scrollbarImage = scrollbarGo.AddComponent<Image>();
            scrollbarImage.color = new Color(1f, 1f, 1f, 0.1f);
            var scrollbar = scrollbarGo.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect
                .ScrollbarVisibility
                .AutoHideAndExpandViewport;

            // TabGroup
            var tabGroupGo = new GameObject("TabGroup");
            tabGroupGo.transform.SetParent(inventoryGo.transform, false);
            var tabGroupRt = tabGroupGo.AddComponent<RectTransform>();
            tabGroupRt.anchorMin = new Vector2(0f, 1f);
            tabGroupRt.anchorMax = new Vector2(1f, 1f);
            tabGroupRt.sizeDelta = new Vector2(0f, 80f);
            tabGroupRt.anchoredPosition = new Vector2(0f, 0f);
            var tabGroup = tabGroupGo.AddComponent<TabGroup>();

            // InventoryView.cs
            var inventory = inventoryGo.AddComponent<InventoryView>();
            SetRef(inventory, "_tabGroup", tabGroup);
            SetRef(inventory, "_slotsRoot", contentRt);

            // ---- DetailPanel ----
            var detailGo = new GameObject("DetailPanel");
            detailGo.transform.SetParent(panelGo.transform, false);
            var detailRt = detailGo.AddComponent<RectTransform>();
            detailRt.anchorMin = new Vector2(0.7f, 0f);
            detailRt.anchorMax = new Vector2(1f, 1f);
            detailRt.offsetMin = Vector2.zero;
            detailRt.offsetMax = Vector2.zero;
            var detailPanel = detailGo.AddComponent<ItemDetailPanel>();
            SetRef(inventory, "_detailPanel", detailPanel);

            // ---- CloseButton ----
            var closeBtn = CreateCloseButton(panelGo.transform);

            panelGo.SetActive(false);

            var stub = panelGo.AddComponent<UIPanelStub>();
            SetRef(stub, "_closeButton", closeBtn);
            return stub;
        }

        /// <summary>
        /// HUD 右上の円形メニューボタン。ラベル無し（アイコン差し替え前提）、色で識別。
        /// </summary>
        private static Button CreateMenuIconButton(
            Transform parent,
            string name,
            float xFromRight,
            Color color
        )
        {
            var btn = CreateButton(
                parent,
                name,
                "",
                anchorMin: new Vector2(1, 1),
                anchorMax: new Vector2(1, 1),
                anchoredPosition: new Vector2(xFromRight, -70),
                sizeDelta: new Vector2(55, 55)
            );
            var image = btn.GetComponent<Image>();
            image.color = color;
            var colors = btn.colors;
            colors.normalColor = color;
            colors.highlightedColor = new Color(
                Mathf.Clamp01(color.r + 0.1f),
                Mathf.Clamp01(color.g + 0.1f),
                Mathf.Clamp01(color.b + 0.1f),
                color.a
            );
            colors.pressedColor = new Color(
                Mathf.Clamp01(color.r - 0.1f),
                Mathf.Clamp01(color.g - 0.1f),
                Mathf.Clamp01(color.b - 0.1f),
                color.a
            );
            btn.colors = colors;
            return btn;
        }

        private static Button CreateCloseButton(Transform parent)
        {
            var btn = CreateButton(
                parent,
                "CloseButton",
                "✕",
                anchorMin: new Vector2(1, 1),
                anchorMax: new Vector2(1, 1),
                anchoredPosition: new Vector2(-50, -50),
                sizeDelta: new Vector2(48, 48)
            );
            var image = btn.GetComponent<Image>();
            var roundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (roundSprite != null)
                image.sprite = roundSprite;
            image.color = new Color(0.55f, 0.3f, 0.35f, 1f);
            var colors = btn.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.75f, 0.4f, 0.45f, 1f);
            colors.pressedColor = new Color(0.45f, 0.2f, 0.25f, 1f);
            btn.colors = colors;
            var label = btn.GetComponentInChildren<Text>();
            if (label != null)
                label.fontSize = 24;
            return btn;
        }

        /// <summary>
        /// キャラクター画面を構築する。
        /// 左: カテゴリリスト(ステータス/武器/装備品) / 右: 各カテゴリのビュー。
        /// タブクリックで Stats/Weapon/Equipment ビューを切替（CharacterUIController）。
        /// </summary>
        private static UIPanelStub CreateCharacterPanel(Transform parent)
        {
            // ---- 全画面背景 ----
            var panelGo = new GameObject("CharacterPanel");
            panelGo.transform.SetParent(parent, false);
            StretchFull(panelGo.AddComponent<RectTransform>());
            var bg = panelGo.AddComponent<Image>();
            var bgSprite = LoadSpriteWithImport(CharacterBgPath);
            if (bgSprite != null)
            {
                bg.sprite = bgSprite;
                bg.color = Color.white;
                bg.preserveAspect = false;
                bg.type = Image.Type.Simple;
            }
            else
            {
                bg.color = new Color(0.1f, 0.12f, 0.18f, 1f);
            }
            bg.raycastTarget = true;

            // ---- 左: カテゴリリスト ----
            var leftGo = new GameObject("CategoryList");
            leftGo.transform.SetParent(panelGo.transform, false);
            var leftRt = leftGo.AddComponent<RectTransform>();
            leftRt.anchorMin = new Vector2(0.5f, 0.5f);
            leftRt.anchorMax = new Vector2(0.5f, 0.5f);
            leftRt.sizeDelta = new Vector2(280, 920);
            leftRt.anchoredPosition = new Vector2(-790, -60);

            var statsTab = CreateCategoryItem(
                leftGo.transform,
                "StatsCategory",
                "ステータス",
                -100,
                true
            );
            var weaponTab = CreateCategoryItem(
                leftGo.transform,
                "WeaponCategory",
                "武器",
                -200,
                false
            );
            var equipmentTab = CreateCategoryItem(
                leftGo.transform,
                "EquipmentCategory",
                "装備品",
                -300,
                false
            );
            var statsLabel = statsTab.GetComponentInChildren<Text>();
            var weaponLabel = weaponTab.GetComponentInChildren<Text>();
            var equipmentLabel = equipmentTab.GetComponentInChildren<Text>();

            // ---- 3つのビュー（タブで切替）----
            var clockSprite = LoadSpriteWithImport(ClockIconPath);
            var statsView = CreateCharacterStatsView(panelGo.transform);
            var weaponView = CreateCharacterWeaponView(panelGo.transform);
            var equipmentRefs = CreateCharacterEquipmentView(panelGo.transform);
            // 初期状態は Stats のみ表示。コントローラ Awake でも上書きされるが念のため。
            weaponView.SetActive(false);
            equipmentRefs.view.SetActive(false);

            // ---- 閉じる(✕) ----
            var closeBtn = CreateCloseButton(panelGo.transform);

            // ---- コントローラ ----
            var charCtrl = panelGo.AddComponent<CharacterUIController>();
            SetRef(charCtrl, "_statsTab", statsTab);
            SetRef(charCtrl, "_weaponTab", weaponTab);
            SetRef(charCtrl, "_equipmentTab", equipmentTab);
            SetRef(charCtrl, "_statsTabLabel", statsLabel);
            SetRef(charCtrl, "_weaponTabLabel", weaponLabel);
            SetRef(charCtrl, "_equipmentTabLabel", equipmentLabel);
            SetRef(charCtrl, "_statsView", statsView);
            SetRef(charCtrl, "_weaponView", weaponView);
            SetRef(charCtrl, "_equipmentView", equipmentRefs.view);
            SetRef(charCtrl, "_equipmentSlot1", equipmentRefs.slot1);
            SetRef(charCtrl, "_equipmentSlot2", equipmentRefs.slot2);
            SetRef(charCtrl, "_equipmentSlot3", equipmentRefs.slot3);
            SetRef(charCtrl, "_equipmentSlot1Icon", equipmentRefs.slot1Icon);
            SetRef(charCtrl, "_equipmentSlot2Icon", equipmentRefs.slot2Icon);
            SetRef(charCtrl, "_equipmentSlot3Icon", equipmentRefs.slot3Icon);
            SetRef(charCtrl, "_equipmentSlot1Empty", equipmentRefs.slot1Empty);
            SetRef(charCtrl, "_equipmentSlot2Empty", equipmentRefs.slot2Empty);
            SetRef(charCtrl, "_equipmentSlot3Empty", equipmentRefs.slot3Empty);
            SetRef(charCtrl, "_equipmentSlot1Frame", equipmentRefs.slot1Frame);
            SetRef(charCtrl, "_equipmentSlot2Frame", equipmentRefs.slot2Frame);
            SetRef(charCtrl, "_equipmentSlot3Frame", equipmentRefs.slot3Frame);
            SetRef(charCtrl, "_equipmentDetailIcon", equipmentRefs.detailIcon);
            SetRef(charCtrl, "_equipmentDetailName", equipmentRefs.detailName);
            SetRef(charCtrl, "_equipmentDetailCategory", equipmentRefs.detailCategory);
            SetRef(charCtrl, "_equipmentDetailStats", equipmentRefs.detailStats);
            SetRef(charCtrl, "_equipmentDetailPassiveTitle", equipmentRefs.detailPassiveTitle);
            SetRef(charCtrl, "_equipmentDetailPassiveDesc", equipmentRefs.detailPassiveDesc);
            SetRef(charCtrl, "_clockIcon", clockSprite);

            panelGo.SetActive(false);

            var stub = panelGo.AddComponent<UIPanelStub>();
            SetRef(stub, "_closeButton", closeBtn);
            return stub;
        }

        private struct EquipmentViewRefs
        {
            public GameObject view;
            public Button slot1,
                slot2,
                slot3;
            public Image slot1Icon,
                slot2Icon,
                slot3Icon;
            public Text slot1Empty,
                slot2Empty,
                slot3Empty;
            public Image slot1Frame,
                slot2Frame,
                slot3Frame;
            public Image detailIcon;
            public Text detailName,
                detailCategory,
                detailStats,
                detailPassiveTitle,
                detailPassiveDesc;
        }

        private struct EquipmentSlotUI
        {
            public Button button;
            public Image frame;
            public Image icon;
            public Text emptyText;
        }

        private static GameObject CreateCharacterStatsView(Transform parent)
        {
            var view = new GameObject("StatsView");
            view.transform.SetParent(parent, false);
            StretchFull(view.AddComponent<RectTransform>());

            var centerGo = CreateViewCenterArea(view.transform);
            CreateText(
                centerGo.transform,
                "CharacterName",
                "プレイヤー（仮）",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                size: new Vector2(700, 60),
                fontSize: 36,
                color: Color.white,
                anchoredPosition: new Vector2(0, -60)
            );
            CreateText(
                centerGo.transform,
                "ModelLabel",
                "Character Model\n（仮プレースホルダー）",
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                size: new Vector2(400, 100),
                fontSize: 28,
                color: new Color(1, 1, 1, 0.5f)
            );

            var rightGo = CreateViewDetailPanel(view.transform);
            CreateText(
                rightGo.transform,
                "SectionTitle",
                "ステータス",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                size: new Vector2(480, 60),
                fontSize: 36,
                color: Color.white,
                anchoredPosition: new Vector2(0, -55)
            );
            CreateDivider(rightGo.transform, -105);
            CreateText(
                rightGo.transform,
                "StatsText",
                "HP             100 / 100\n\n"
                    + "攻撃力             0\n\n"
                    + "防御力             0\n\n"
                    + "移動速度           0\n\n"
                    + "攻撃速度           0\n\n"
                    + "会心率             0 %\n\n"
                    + "会心ダメージ       0 %",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                size: new Vector2(440, 640),
                fontSize: 24,
                color: new Color(0.85f, 0.9f, 1f, 1f),
                anchoredPosition: new Vector2(0, -440)
            );

            return view;
        }

        private static GameObject CreateCharacterWeaponView(Transform parent)
        {
            var view = new GameObject("WeaponView");
            view.transform.SetParent(parent, false);
            StretchFull(view.AddComponent<RectTransform>());

            var centerGo = CreateViewCenterArea(view.transform);
            CreateText(
                centerGo.transform,
                "WeaponName",
                "暁の剣（仮）",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                size: new Vector2(700, 60),
                fontSize: 36,
                color: Color.white,
                anchoredPosition: new Vector2(0, -60)
            );
            CreateText(
                centerGo.transform,
                "WeaponModelLabel",
                "Weapon Model\n（仮プレースホルダー）",
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                size: new Vector2(400, 100),
                fontSize: 28,
                color: new Color(1, 1, 1, 0.5f)
            );

            var rightGo = CreateViewDetailPanel(view.transform);
            CreateText(
                rightGo.transform,
                "SectionTitle",
                "武器",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                size: new Vector2(480, 60),
                fontSize: 36,
                color: Color.white,
                anchoredPosition: new Vector2(0, -55)
            );
            CreateText(
                rightGo.transform,
                "WeaponType",
                "片手剣  ★★★★★",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                size: new Vector2(480, 36),
                fontSize: 22,
                color: new Color(0.95f, 0.8f, 0.4f, 1f),
                anchoredPosition: new Vector2(0, -110)
            );
            CreateDivider(rightGo.transform, -155);
            CreateText(
                rightGo.transform,
                "WeaponStats",
                "基礎攻撃力        565\n\n会心率           +27.6%",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                size: new Vector2(440, 130),
                fontSize: 24,
                color: new Color(0.85f, 0.9f, 1f, 1f),
                anchoredPosition: new Vector2(0, -250)
            );
            CreateDivider(rightGo.transform, -370);
            CreateText(
                rightGo.transform,
                "Refinement",
                "精錬ランク Lv.1",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                size: new Vector2(440, 36),
                fontSize: 22,
                color: new Color(0.95f, 0.8f, 0.4f, 1f),
                anchoredPosition: new Vector2(0, -410)
            );
            CreateText(
                rightGo.transform,
                "PassiveTitle",
                "パッシブ「黎明の誓い」",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                size: new Vector2(440, 36),
                fontSize: 24,
                color: Color.white,
                anchoredPosition: new Vector2(0, -475)
            );
            CreateText(
                rightGo.transform,
                "PassiveDesc",
                "攻撃時、会心率が +10% 上昇する\n（最大3層、6秒持続）",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                size: new Vector2(440, 80),
                fontSize: 22,
                color: new Color(0.7f, 0.95f, 0.75f, 1f),
                anchoredPosition: new Vector2(0, -555)
            );
            CreateTextOnlyButton(
                rightGo.transform,
                "ChangeWeaponButton",
                "▶ 武器を変更",
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                anchoredPosition: new Vector2(0, 60),
                sizeDelta: new Vector2(280, 50),
                fontSize: 26,
                color: new Color(0.95f, 0.8f, 0.4f, 1f)
            );

            return view;
        }

        private static Button CreateTextOnlyButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            int fontSize,
            Color color
        )
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPosition;

            // 透明クリック領域
            var image = go.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0);
            image.raycastTarget = true;

            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            StretchFull(labelGo.AddComponent<RectTransform>());
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = label;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = fontSize;
            labelText.color = color;
            labelText.raycastTarget = false;

            return button;
        }

        private static EquipmentViewRefs CreateCharacterEquipmentView(Transform parent)
        {
            var view = new GameObject("EquipmentView");
            view.transform.SetParent(parent, false);
            StretchFull(view.AddComponent<RectTransform>());

            // 中央: キャラモデル placeholder + 3スロット横並び
            var centerGo = CreateViewCenterArea(view.transform);
            CreateText(
                centerGo.transform,
                "CharacterName",
                "プレイヤー（仮）",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                size: new Vector2(700, 60),
                fontSize: 36,
                color: Color.white,
                anchoredPosition: new Vector2(0, -60)
            );
            CreateText(
                centerGo.transform,
                "ModelLabel",
                "Character Model\n（仮プレースホルダー）",
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                size: new Vector2(400, 100),
                fontSize: 28,
                color: new Color(1, 1, 1, 0.5f),
                anchoredPosition: new Vector2(0, 80)
            );

            // 3スロット: x=-195/0/+195、y=-280
            var slot1UI = CreateEquipmentSlot(
                centerGo.transform,
                "EquipmentSlot1",
                new Vector2(-195, -280)
            );
            var slot2UI = CreateEquipmentSlot(
                centerGo.transform,
                "EquipmentSlot2",
                new Vector2(0, -280)
            );
            var slot3UI = CreateEquipmentSlot(
                centerGo.transform,
                "EquipmentSlot3",
                new Vector2(195, -280)
            );

            // 右: 詳細欄
            var rightGo = CreateViewDetailPanel(view.transform);
            CreateText(
                rightGo.transform,
                "SectionTitle",
                "装備品",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                size: new Vector2(480, 60),
                fontSize: 36,
                color: Color.white,
                anchoredPosition: new Vector2(0, -55)
            );
            CreateDivider(rightGo.transform, -105);

            // 詳細アイコン（大）
            var detailIconGo = new GameObject("DetailIcon");
            detailIconGo.transform.SetParent(rightGo.transform, false);
            var detailIconRt = detailIconGo.AddComponent<RectTransform>();
            detailIconRt.anchorMin = new Vector2(0.5f, 1f);
            detailIconRt.anchorMax = new Vector2(0.5f, 1f);
            detailIconRt.sizeDelta = new Vector2(200, 200);
            detailIconRt.anchoredPosition = new Vector2(0, -230);
            var detailIcon = detailIconGo.AddComponent<Image>();
            detailIcon.preserveAspect = true;
            detailIcon.color = new Color(0, 0, 0, 0);
            detailIcon.raycastTarget = false;

            var detailName = CreateText(
                rightGo.transform,
                "DetailName",
                "",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                size: new Vector2(480, 50),
                fontSize: 32,
                color: Color.white,
                anchoredPosition: new Vector2(0, -360)
            );
            var detailCategory = CreateText(
                rightGo.transform,
                "DetailCategory",
                "",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                size: new Vector2(480, 36),
                fontSize: 22,
                color: new Color(0.95f, 0.8f, 0.4f, 1f),
                anchoredPosition: new Vector2(0, -405)
            );
            CreateDivider(rightGo.transform, -445);
            var detailStats = CreateText(
                rightGo.transform,
                "DetailStats",
                "",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                size: new Vector2(440, 36),
                fontSize: 22,
                color: new Color(0.85f, 0.9f, 1f, 1f),
                anchoredPosition: new Vector2(0, -485)
            );
            CreateDivider(rightGo.transform, -530);
            var detailPassiveTitle = CreateText(
                rightGo.transform,
                "DetailPassiveTitle",
                "",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                size: new Vector2(440, 36),
                fontSize: 24,
                color: Color.white,
                anchoredPosition: new Vector2(0, -575)
            );
            var detailPassiveDesc = CreateText(
                rightGo.transform,
                "DetailPassiveDesc",
                "",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                size: new Vector2(440, 80),
                fontSize: 22,
                color: new Color(0.7f, 0.95f, 0.75f, 1f),
                anchoredPosition: new Vector2(0, -650)
            );

            return new EquipmentViewRefs
            {
                view = view,
                slot1 = slot1UI.button,
                slot2 = slot2UI.button,
                slot3 = slot3UI.button,
                slot1Icon = slot1UI.icon,
                slot2Icon = slot2UI.icon,
                slot3Icon = slot3UI.icon,
                slot1Empty = slot1UI.emptyText,
                slot2Empty = slot2UI.emptyText,
                slot3Empty = slot3UI.emptyText,
                slot1Frame = slot1UI.frame,
                slot2Frame = slot2UI.frame,
                slot3Frame = slot3UI.frame,
                detailIcon = detailIcon,
                detailName = detailName,
                detailCategory = detailCategory,
                detailStats = detailStats,
                detailPassiveTitle = detailPassiveTitle,
                detailPassiveDesc = detailPassiveDesc,
            };
        }

        private static EquipmentSlotUI CreateEquipmentSlot(
            Transform parent,
            string name,
            Vector2 position
        )
        {
            var slotGo = new GameObject(name);
            slotGo.transform.SetParent(parent, false);
            var rt = slotGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(160, 160);
            rt.anchoredPosition = position;

            // フレーム兼クリック領域（薄い背景）
            var frame = slotGo.AddComponent<Image>();
            frame.color = new Color(1f, 1f, 1f, 0.15f);
            frame.raycastTarget = true;

            var button = slotGo.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = frame;

            // アイコン（中身）
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(slotGo.transform, false);
            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(10, 10);
            iconRt.offsetMax = new Vector2(-10, -10);
            var iconImage = iconGo.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.color = new Color(0, 0, 0, 0);
            iconImage.raycastTarget = false;

            // 空スロット表示（"+"）
            var emptyGo = new GameObject("EmptyText");
            emptyGo.transform.SetParent(slotGo.transform, false);
            var emptyRt = emptyGo.AddComponent<RectTransform>();
            emptyRt.anchorMin = new Vector2(0.5f, 0.5f);
            emptyRt.anchorMax = new Vector2(0.5f, 0.5f);
            emptyRt.sizeDelta = new Vector2(80, 80);
            var emptyText = emptyGo.AddComponent<Text>();
            emptyText.text = "+";
            emptyText.alignment = TextAlignmentOptions.Center;
            emptyText.fontSize = 60;
            emptyText.color = new Color(1, 1, 1, 0.35f);
            emptyText.raycastTarget = false;

            return new EquipmentSlotUI
            {
                button = button,
                frame = frame,
                icon = iconImage,
                emptyText = emptyText,
            };
        }

        private static GameObject CreateViewCenterArea(Transform parent)
        {
            var go = new GameObject("ModelArea");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(800, 920);
            rt.anchoredPosition = new Vector2(-180, -60);
            return go;
        }

        private static GameObject CreateViewDetailPanel(Transform parent)
        {
            var go = new GameObject("DetailPanel");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(540, 920);
            rt.anchoredPosition = new Vector2(570, -60);
            return go;
        }

        private static void CreateDivider(Transform parent, float y)
        {
            var dividerGo = new GameObject("Divider");
            dividerGo.transform.SetParent(parent, false);
            var dividerRt = dividerGo.AddComponent<RectTransform>();
            dividerRt.anchorMin = new Vector2(0.5f, 1f);
            dividerRt.anchorMax = new Vector2(0.5f, 1f);
            dividerRt.sizeDelta = new Vector2(480, 2);
            dividerRt.anchoredPosition = new Vector2(0, y);
            dividerGo.AddComponent<Image>().color = new Color(1, 1, 1, 0.3f);
        }

        /// <summary>
        /// Character 画面の左サイドメニュー用テキストボタン。背景なし、テキスト色で active 状態を表現。
        /// </summary>
        private static Button CreateCategoryItem(
            Transform parent,
            string name,
            string label,
            float yOffset,
            bool isActive
        )
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(220, 70);
            rt.anchoredPosition = new Vector2(0, yOffset);

            // 透明背景（クリック領域確保のみ）
            var image = go.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0);
            image.raycastTarget = true;

            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            StretchFull(labelGo.AddComponent<RectTransform>());
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = label;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = 30;
            labelText.color = isActive
                ? new Color(0.55f, 0.75f, 1f, 1f)
                : new Color(0.75f, 0.75f, 0.8f, 1f);
            labelText.raycastTarget = false;

            return button;
        }

        private static Button CreateTabButton(
            Transform parent,
            string name,
            string label,
            float xOffset,
            bool isActive
        )
        {
            var btn = CreateButton(
                parent,
                name,
                label,
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                anchoredPosition: new Vector2(xOffset, -75),
                sizeDelta: new Vector2(65, 65)
            );

            // 丸い見た目にする(後でアイコンに差し替え予定)
            var image = btn.GetComponent<Image>();
            var roundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (roundSprite != null)
                image.sprite = roundSprite;

            // ラベルは仮表示。アイコンに差し替える時は削除。
            var labelText = btn.GetComponentInChildren<Text>();
            if (labelText != null)
                labelText.fontSize = 20;

            if (isActive)
            {
                image.color = new Color(0.55f, 0.7f, 0.95f, 1f);
                var colors = btn.colors;
                colors.normalColor = image.color;
                colors.highlightedColor = new Color(0.65f, 0.8f, 1f, 1f);
                btn.colors = colors;
            }
            return btn;
        }

        private static void CreateItemSlot(Transform parent, int index, Sprite icon = null)
        {
            var slotGo = new GameObject($"Slot_{index:00}");
            slotGo.transform.SetParent(parent, false);
            slotGo.AddComponent<RectTransform>();
            var image = slotGo.AddComponent<Image>();
            image.preserveAspect = true;
            if (icon != null)
            {
                image.sprite = icon;
                image.color = Color.white;
            }
            else
            {
                // 空スロット: 完全透明（クリック判定だけ残す）
                image.color = new Color(0, 0, 0, 0);
            }
            image.raycastTarget = true;
            var button = slotGo.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
        }

        private static Slider CreateProgressBar(Transform parent)
        {
            var sliderGo = new GameObject("ProgressBar");
            sliderGo.transform.SetParent(parent, false);
            var sliderRt = sliderGo.AddComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0.2f, 0.08f);
            sliderRt.anchorMax = new Vector2(0.8f, 0.10f);
            sliderRt.offsetMin = Vector2.zero;
            sliderRt.offsetMax = Vector2.zero;

            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(sliderGo.transform, false);
            StretchFull(bgGo.AddComponent<RectTransform>());
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(1f, 1f, 1f, 0.15f);
            bgImage.raycastTarget = false;

            var fillAreaGo = new GameObject("Fill Area");
            fillAreaGo.transform.SetParent(sliderGo.transform, false);
            StretchFull(fillAreaGo.AddComponent<RectTransform>());

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            StretchFull(fillGo.AddComponent<RectTransform>());
            var fillImage = fillGo.AddComponent<Image>();
            fillImage.color = Color.white;
            fillImage.raycastTarget = false;

            var slider = sliderGo.AddComponent<Slider>();
            slider.fillRect = fillGo.GetComponent<RectTransform>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            return slider;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static void EnsureInputSystemEventSystem()
        {
            var existing = Object.FindAnyObjectByType<EventSystem>();
            if (existing == null)
            {
                CreateEventSystem();
                return;
            }
            var legacy = existing.GetComponent<StandaloneInputModule>();
            if (legacy != null)
                Object.DestroyImmediate(legacy);
            if (existing.GetComponent<InputSystemUIInputModule>() == null)
            {
                existing.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        private static Sprite LoadSpriteWithImport(string path)
        {
            if (!File.Exists(path))
                return null;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void SetRef(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError(
                    $"[SetupInitialScenes] SerializedProperty '{fieldName}' not found on {target.GetType().Name}."
                );
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetStr(Object target, string fieldName, string value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError(
                    $"[SetupInitialScenes] SerializedProperty '{fieldName}' not found on {target.GetType().Name}."
                );
                return;
            }
            prop.stringValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
#endif
