using System;
using CreativeAI.UI.ConversationUI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CreativeAI.UI.ConversationUI.Editor
{
    /// <summary>
    /// 会話UIの Prefab と確認用シーン UI_ConversationPreview を一括生成する Editor ツール。
    /// 立ち絵/ウィンドウ画像を Sprite として import し、ConversationView.prefab を組み立て、
    /// UI_ConversationPreview シーンにその実体 + プレビュー駆動役を配置する。手書き YAML を避け Unity に正しく
    /// シリアライズさせるための道具(メニューからも実行可)。
    /// </summary>
    public static class ConversationUIBuilder
    {
        [InitializeOnLoadMethod]
        private static void UpgradePrefabLayoutIfNeeded()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            QueuePrefabUpgrade();
        }

        private static void HandlePlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                QueuePrefabUpgrade();
        }

        private static void QueuePrefabUpgrade()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (
                    prefab != null
                    && prefab.transform.Find("ContextGuide") != null
                    && prefab.transform.Find("ItemRewardBackdrop") != null
                    && prefab.transform.Find("AUTOAButton") != null
                    && prefab.transform.Find("DialogueHistoryPanel") != null
                    && prefab.transform.Find("Layout/_System/ConversationArchiveV11") != null
                    && HasPreviewRewardReferences(prefab)
                )
                    return;

                ImportAsSprite(WindowPng);
                ImportAsSprite(ContinueButtonPng);
                ImportAsSprite(ChoiceButtonPng);
                ImportAsSprite(ItemPreviewPng);
                BuildPrefab();
                AssetDatabase.SaveAssets();
                Debug.Log(
                    "[ConversationUIBuilder] ConversationView Prefab を最新UI構成へ更新しました。"
                );
            };
        }

        private const string ConversationArtPath = "Assets/_Project/Art/UI/Conversation/";
        private const string WindowPng = ConversationArtPath + "ConversationWindow.png";
        private const string ContinueButtonPng =
            ConversationArtPath + "ConversationContinueButton.png";
        private const string ChoiceButtonPng = ConversationArtPath + "ConversationChoiceButton.png";
        private const string ItemPreviewPng =
            "Assets/_Project/Art/UI/Items/Food/PreCraft/item_food_apple.png";
        private const string WeaponPreviewPrefab = "Assets/_Project/Art/Models/Weapons/Katana.glb";
        private const string CharacterArtPath = ConversationArtPath + "Character/";
        private static readonly (string Key, string Path, DialoguePortraitSide Side)[] Portraits =
        {
            (
                "protagonist_normal",
                CharacterArtPath + "Protagonist/Portraits/Protagonist_Normal.png",
                DialoguePortraitSide.Left
            ),
            (
                "robot_normal",
                CharacterArtPath + "Robot/Portraits/Robot_Normal.png",
                DialoguePortraitSide.Right
            ),
            (
                "fragile_girl_normal",
                CharacterArtPath + "FragileGirl/Portraits/FragileGirl_Normal.png",
                DialoguePortraitSide.Right
            ),
            (
                "fragile_girl_worried_smile",
                CharacterArtPath + "FragileGirl/Portraits/FragileGirl_WorriedSmile.png",
                DialoguePortraitSide.Right
            ),
            (
                "fragile_girl_frightened",
                CharacterArtPath + "FragileGirl/Portraits/FragileGirl_Frightened.png",
                DialoguePortraitSide.Right
            ),
            (
                "fragile_girl_smile",
                CharacterArtPath + "FragileGirl/Portraits/FragileGirl_Smile.png",
                DialoguePortraitSide.Right
            ),
            (
                "fragile_girl_determined",
                CharacterArtPath + "FragileGirl/Portraits/FragileGirl_Determined.png",
                DialoguePortraitSide.Right
            ),
            (
                "fragile_girl_surprised",
                CharacterArtPath + "FragileGirl/Portraits/FragileGirl_Surprised.png",
                DialoguePortraitSide.Right
            ),
            (
                "gramophone_normal",
                CharacterArtPath + "Gramophone/Portraits/Gramophone_Normal.png",
                DialoguePortraitSide.Right
            ),
        };
        private const string FontPath =
            "Assets/_Project/Art/UI/Fonts/NotoSansJP-VariableFont_wght SDF.asset";
        private const string PrefabPath =
            "Assets/_Project/Features/UI/ConversationUI/Prefabs/ConversationView.prefab";
        private static readonly string[] CharacterDefinitionPaths =
        {
            "Assets/_Project/Features/UI/ConversationUI/Data/Characters/Protagonist.asset",
            "Assets/_Project/Features/UI/ConversationUI/Data/Characters/Robot.asset",
            "Assets/_Project/Features/UI/ConversationUI/Data/Characters/FragileGirl.asset",
            "Assets/_Project/Features/UI/ConversationUI/Data/Characters/Gramophone.asset",
        };
        private const string ScenePath = "Assets/_Project/Scenes/UI/UI_ConversationPreview.unity";

        [MenuItem("Tools/CreativeAI/Build Conversation UI")]
        public static void BuildAll()
        {
            ImportAsSprite(WindowPng);
            ImportAsSprite(ContinueButtonPng);
            ImportAsSprite(ChoiceButtonPng);
            ImportAsSprite(ItemPreviewPng);
            foreach (var portrait in Portraits)
            {
                ImportAsSprite(portrait.Path);
                ImportAsSprite(GetIconPath(portrait.Path));
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var prefab = BuildPrefab();
            BuildScene(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ConversationUIBuilder] 生成完了: {PrefabPath} / {ScenePath}");
        }

        private static void ImportAsSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[ConversationUIBuilder] TextureImporter が取れません: {path}");
                return;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed; // UI はブロック圧縮ノイズを避け RGBA32 で取り込む
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static string GetIconPath(string portraitPath) =>
            portraitPath.Replace("/Portraits/", "/Icons/").Replace(".png", "_Icon.png");

        private static GameObject BuildPrefab()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var windowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WindowPng);
            var continueButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ContinueButtonPng);
            var choiceButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ChoiceButtonPng);
            var portraitSprites = new Sprite[Portraits.Length];
            for (int i = 0; i < Portraits.Length; i++)
                portraitSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(Portraits[i].Path);
            var portraitSprite = portraitSprites[0];

            // --- ルート(Canvas + 常駐コンポーネント) ---
            var root = new GameObject(
                "ConversationView",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup),
                typeof(ConversationView),
                typeof(DialogueHistoryPanel)
            );
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // HUD/各パネルより前面

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100;

            var layout = CreateLayer("Layout", root.transform);
            var portraitLayer = CreateLayer("Portraits", layout.transform);
            var dialogueLayer = CreateLayer("Dialogue", layout.transform);
            var rewardLayer = CreateLayer("Rewards", layout.transform);
            var hudLayer = CreateLayer("HUD", layout.transform);
            var controlBar = CreateLayer("Controls", hudLayer.transform);
            var controlBarGroup = controlBar.AddComponent<CanvasGroup>();
            var historyLayer = CreateLayer("History", layout.transform);
            var systemLayer = CreateLayer("_System", layout.transform);
            CreateUI("ConversationArchiveV11", systemLayer.transform).SetActive(false);

            // --- 立ち絵 ---
            var portrait = CreateUI("Portrait", portraitLayer.transform);
            var portraitImg = portrait.AddComponent<Image>();
            portraitImg.sprite = portraitSprite;
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget = false;
            // 参考レイアウトに合わせ、画面右側で足元を画面外へ逃がし、ウィンドウの背面に立たせる。
            SetAnchored(
                portrait,
                new Vector2(0.12f, 0),
                new Vector2(0.12f, 0),
                new Vector2(0.5f, 0),
                new Vector2(0, -60),
                new Vector2(650, 1170)
            );
            var rightPortrait = Object.Instantiate(portrait, portraitLayer.transform);
            rightPortrait.name = "PortraitRight";
            var rightPortraitImage = rightPortrait.GetComponent<Image>();
            rightPortraitImage.enabled = false;
            SetAnchored(
                rightPortrait,
                new Vector2(0.88f, 0f),
                new Vector2(0.88f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, -60f),
                new Vector2(650f, 1170f)
            );
            rightPortrait.transform.localScale = new Vector3(-1f, 1f, 1f);

            // --- ウィンドウ ---
            var window = CreateUI("Window", dialogueLayer.transform);
            var windowImg = window.AddComponent<Image>();
            windowImg.sprite = windowSprite;
            windowImg.preserveAspect = true;
            windowImg.raycastTarget = true; // クリックで送るための当たり判定
            // トリミング済みの正式画像を基準解像度ではほぼ等倍で画面下部に配置する。
            SetAnchored(
                window,
                new Vector2(0.5f, 0),
                new Vector2(0.5f, 0),
                new Vector2(0.5f, 0),
                new Vector2(0, 70),
                new Vector2(1301, 283)
            );

            // 名前プレート(ウィンドウ左上の暗いバー)
            var nameText = CreateText(
                "NameText",
                window.transform,
                font,
                "冒険者",
                36,
                new Color(1f, 1f, 1f, 1f),
                TextAlignmentOptions.Left
            );
            SetStretch(nameText, new Vector2(0.025f, 0.69f), new Vector2(0.38f, 0.97f));
            var nameLabel = nameText.GetComponent<TMP_Text>();
            nameLabel.enableAutoSizing = true;
            nameLabel.fontSizeMin = 24f;
            nameLabel.fontSizeMax = 36f;
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;

            // 本文(明るい本体)
            var bodyText = CreateText(
                "BodyText",
                window.transform,
                font,
                "ここに会話テキストが表示されます。クリックまたはスペース/Enter/Zキーで送れます。",
                34,
                new Color(1f, 1f, 1f, 1f),
                TextAlignmentOptions.MidlineLeft
            );
            SetStretch(bodyText, new Vector2(0.12f, 0.14f), new Vector2(0.95f, 0.64f));
            var bodyLabel = bodyText.GetComponent<TMP_Text>();
            bodyLabel.enableAutoSizing = true;
            bodyLabel.fontSizeMin = 24f;
            bodyLabel.fontSizeMax = 34f;
            bodyLabel.textWrappingMode = TextWrappingModes.Normal;

            // 送り待ちに小さく上下するインジケーター。正式画像のピクセル比を維持する。
            var nextIndicator = CreateUI("NextIndicator", window.transform);
            var nextIndicatorImage = nextIndicator.AddComponent<Image>();
            nextIndicatorImage.sprite = continueButtonSprite;
            nextIndicatorImage.preserveAspect = true;
            nextIndicatorImage.raycastTarget = false;
            SetAnchored(
                nextIndicator,
                new Vector2(0.94f, 0.13f),
                new Vector2(0.94f, 0.13f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(36, 34)
            );

            var autoModeIndicator = CreateText(
                "AutoModeIndicator",
                hudLayer.transform,
                font,
                "AUTO",
                28,
                new Color(0.75f, 0.9f, 1f, 1f),
                TextAlignmentOptions.Center
            );
            autoModeIndicator.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
            SetAnchored(
                autoModeIndicator,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-36f, -28f),
                new Vector2(180f, 52f)
            );
            autoModeIndicator.SetActive(false);

            var autoProgressTrack = CreateUI("AutoProgressTrack", autoModeIndicator.transform);
            var autoTrackImage = autoProgressTrack.AddComponent<Image>();
            autoTrackImage.color = new Color(1f, 1f, 1f, 0.28f);
            autoTrackImage.raycastTarget = false;
            SetStretch(autoProgressTrack, new Vector2(0.1f, -0.12f), new Vector2(0.9f, 0f));
            var autoProgressFill = CreateUI("Fill", autoProgressTrack.transform);
            var autoFillImage = autoProgressFill.AddComponent<Image>();
            autoFillImage.color = new Color(0.5f, 0.85f, 1f, 1f);
            autoFillImage.raycastTarget = false;
            SetStretch(autoProgressFill, Vector2.zero, Vector2.one);

            var controlGuide = CreateText(
                "ContextGuide",
                hudLayer.transform,
                font,
                "NEXT   ENTER / SPACE",
                20,
                new Color(1f, 1f, 1f, 0.68f),
                TextAlignmentOptions.BottomLeft
            );
            SetAnchored(
                controlGuide,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(26f, 20f),
                new Vector2(520f, 38f)
            );

            var autoControl = CreateDockButton(
                controlBar.transform,
                font,
                "AUTO",
                "A",
                -564f,
                104f,
                "会話を自動で送ります"
            );
            var skipControl = CreateDockButton(
                controlBar.transform,
                font,
                "SKIP",
                "S",
                -452f,
                104f,
                "既読の会話を高速で進めます"
            );
            var speedControl = CreateDockButton(
                controlBar.transform,
                font,
                "SPEED",
                "T",
                -324f,
                136f,
                "文章の表示速度を切り替えます"
            );
            var hideControl = CreateDockButton(
                controlBar.transform,
                font,
                "HIDE",
                "H",
                -196f,
                104f,
                "会話ウィンドウを一時的に隠します"
            );
            var tooltipPanel = CreateUI("ControlTooltip", hudLayer.transform);
            var tooltipBackground = tooltipPanel.AddComponent<Image>();
            tooltipBackground.color = new Color(0.025f, 0.035f, 0.055f, 0.96f);
            tooltipBackground.raycastTarget = false;
            var tooltipGroup = tooltipPanel.AddComponent<CanvasGroup>();
            tooltipGroup.alpha = 0f;
            tooltipGroup.interactable = false;
            tooltipGroup.blocksRaycasts = false;
            SetAnchored(
                tooltipPanel,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-324f, 78f),
                new Vector2(300f, 38f)
            );
            var tooltipText = CreateText(
                "Label",
                tooltipPanel.transform,
                font,
                string.Empty,
                17f,
                new Color(0.82f, 0.9f, 1f, 0.94f),
                TextAlignmentOptions.Center
            );
            SetStretch(tooltipText, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f));
            controlBar
                .AddComponent<ConversationControlBar>()
                .Configure(controlBarGroup, tooltipText.GetComponent<TMP_Text>(), tooltipGroup);
            var speedToast = CreateText(
                "SpeedToast",
                hudLayer.transform,
                font,
                "TEXT SPEED  x1",
                18f,
                new Color(0.72f, 0.88f, 1f, 1f),
                TextAlignmentOptions.Center
            );
            speedToast.AddComponent<CanvasGroup>().alpha = 0f;
            SetAnchored(
                speedToast,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-324f, 66f),
                new Vector2(210f, 34f)
            );

            var itemBackdrop = CreateRewardImage(
                "ItemRewardBackdrop",
                rewardLayer.transform,
                new Color(0.025f, 0.035f, 0.055f, 0.88f),
                new Vector2(406f, 366f)
            );
            itemBackdrop.gameObject.AddComponent<Outline>().effectColor = new Color(
                0.55f,
                0.72f,
                1f,
                0.5f
            );
            var itemReward = CreateRewardImage(
                "ItemRewardImage",
                rewardLayer.transform,
                Color.white,
                new Vector2(256f, 256f)
            );
            itemReward.preserveAspect = true;
            var weaponBackdrop = CreateRewardImage(
                "WeaponRewardBackdrop",
                rewardLayer.transform,
                new Color(0f, 0f, 0f, 0.4f),
                new Vector2(640f, 360f)
            );
            var weaponRewardObject = CreateUI("WeaponRewardImage", rewardLayer.transform);
            var weaponReward = weaponRewardObject.AddComponent<RawImage>();
            weaponReward.raycastTarget = false;
            SetAnchored(
                weaponRewardObject,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 220f),
                new Vector2(640f, 360f)
            );
            itemBackdrop.gameObject.SetActive(false);
            itemReward.gameObject.SetActive(false);
            weaponBackdrop.gameObject.SetActive(false);
            weaponRewardObject.SetActive(false);

            // 選択肢コンテナ(通常は非表示)
            var choiceContainer = CreateUI("ChoiceContainer", window.transform);
            var vlg = choiceContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 38;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            SetAnchored(
                choiceContainer,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0),
                new Vector2(0, 64),
                new Vector2(565, 286)
            );

            // 選択肢ボタンの雛形(非active。実行時に複製)
            var choiceTemplate = CreateUI("ChoiceButtonTemplate", choiceContainer.transform);
            var btnImg = choiceTemplate.AddComponent<Image>();
            btnImg.sprite = choiceButtonSprite;
            btnImg.preserveAspect = true;
            var button = choiceTemplate.AddComponent<Button>();
            button.targetGraphic = btnImg;
            var le = choiceTemplate.AddComponent<LayoutElement>();
            le.minHeight = 70;
            le.preferredHeight = 70;
            var choiceLabel = CreateText(
                "Label",
                choiceTemplate.transform,
                font,
                "選択肢",
                32,
                new Color(1f, 1f, 1f, 1f),
                TextAlignmentOptions.Center
            );
            SetStretch(choiceLabel, new Vector2(0.08f, 0), new Vector2(0.92f, 1));
            choiceTemplate.SetActive(false);

            // --- ConversationView の直列フィールドを配線 ---
            var view = root.GetComponent<ConversationView>();
            var so = new SerializedObject(view);
            so.FindProperty("_root").objectReferenceValue = root.GetComponent<CanvasGroup>();
            so.FindProperty("_windowRoot").objectReferenceValue =
                window.GetComponent<RectTransform>();
            so.FindProperty("_portrait").objectReferenceValue = portraitImg;
            so.FindProperty("_rightPortrait").objectReferenceValue = rightPortraitImage;
            so.FindProperty("_nameText").objectReferenceValue = nameText.GetComponent<TMP_Text>();
            so.FindProperty("_bodyText").objectReferenceValue = bodyText.GetComponent<TMP_Text>();
            so.FindProperty("_nextIndicator").objectReferenceValue = nextIndicator;
            so.FindProperty("_autoModeIndicator").objectReferenceValue =
                autoModeIndicator.GetComponent<TMP_Text>();
            so.FindProperty("_controlGuide").objectReferenceValue =
                controlGuide.GetComponent<TMP_Text>();
            so.FindProperty("_autoProgressFill").objectReferenceValue = autoFillImage;
            so.FindProperty("_historyPanel").objectReferenceValue =
                root.GetComponent<DialogueHistoryPanel>();
            so.FindProperty("_choiceContainer").objectReferenceValue =
                choiceContainer.GetComponent<RectTransform>();
            so.FindProperty("_choiceButtonTemplate").objectReferenceValue = button;
            so.FindProperty("_defaultPortrait").objectReferenceValue = portraitSprite;
            so.FindProperty("_itemGetSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(ItemPreviewPng);
            so.FindProperty("_weaponModelPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPreviewPrefab);
            so.FindProperty("_itemRewardImage").objectReferenceValue = itemReward;
            so.FindProperty("_itemRewardBackdrop").objectReferenceValue = itemBackdrop;
            so.FindProperty("_weaponRewardImage").objectReferenceValue = weaponReward;
            so.FindProperty("_weaponRewardBackdrop").objectReferenceValue = weaponBackdrop;
            so.FindProperty("_autoControlButton").objectReferenceValue = autoControl;
            so.FindProperty("_skipControlButton").objectReferenceValue = skipControl;
            so.FindProperty("_speedControlButton").objectReferenceValue = speedControl;
            so.FindProperty("_speedControlLabel").objectReferenceValue = speedControl
                .transform.Find("Label")
                .GetComponent<TMP_Text>();
            so.FindProperty("_speedToast").objectReferenceValue =
                speedToast.GetComponent<TMP_Text>();
            so.FindProperty("_hideControlButton").objectReferenceValue = hideControl;

            var characters = so.FindProperty("_characters");
            characters.arraySize = CharacterDefinitionPaths.Length;
            for (int i = 0; i < CharacterDefinitionPaths.Length; i++)
            {
                characters.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<DialogueCharacterDefinition>(
                        CharacterDefinitionPaths[i]
                    );
            }

            var portraits = so.FindProperty("_portraits");
            portraits.arraySize = Portraits.Length;
            for (int i = 0; i < Portraits.Length; i++)
            {
                var entry = portraits.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Key").stringValue = Portraits[i].Key;
                entry.FindPropertyRelative("Sprite").objectReferenceValue = portraitSprites[i];
                entry.FindPropertyRelative("Side").enumValueIndex = (int)Portraits[i].Side;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            root.GetComponent<DialogueHistoryPanel>().BuildPrefabView(font);
            root.transform.Find("DialogueHistoryButton").SetParent(controlBar.transform, false);
            root.transform.Find("DialogueHistoryPanel").SetParent(historyLayer.transform, false);
            ValidateRequiredReferences(root.GetComponent<ConversationView>());
            ValidateHistoryReferences(root.GetComponent<DialogueHistoryPanel>());
            ValidateNoMissingScripts(root);

            // --- Prefab 保存 ---
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static bool HasPreviewRewardReferences(GameObject prefab)
        {
            var view = prefab != null ? prefab.GetComponent<ConversationView>() : null;
            if (view == null)
                return false;
            var serializedView = new SerializedObject(view);
            return serializedView.FindProperty("_itemGetSprite").objectReferenceValue != null
                && serializedView.FindProperty("_weaponModelPrefab").objectReferenceValue != null;
        }

        private static void BuildScene(GameObject prefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.10f, 0.13f, 1f);
            cam.orthographic = true;
            camGo.transform.position = new Vector3(0, 0, -10);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetAsLastSibling();

            var driverGo = new GameObject(
                "ConversationPreviewDriver",
                typeof(ConversationPreviewDriver)
            );
            var driver = driverGo.GetComponent<ConversationPreviewDriver>();
            var dso = new SerializedObject(driver);
            dso.FindProperty("_view").objectReferenceValue =
                instance.GetComponent<ConversationView>();
            dso.ApplyModifiedPropertiesWithoutUndo();

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ---- helpers ----

        private static GameObject CreateUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject CreateLayer(string name, Transform parent)
        {
            var layer = CreateUI(name, parent);
            SetStretch(layer, Vector2.zero, Vector2.one);
            return layer;
        }

        private static void ValidateRequiredReferences(ConversationView view)
        {
            var serializedView = new SerializedObject(view);
            string[] requiredReferences =
            {
                "_root",
                "_windowRoot",
                "_portrait",
                "_rightPortrait",
                "_nameText",
                "_bodyText",
                "_nextIndicator",
                "_autoModeIndicator",
                "_controlGuide",
                "_autoProgressFill",
                "_historyPanel",
                "_choiceContainer",
                "_choiceButtonTemplate",
                "_itemRewardImage",
                "_itemRewardBackdrop",
                "_weaponRewardImage",
                "_weaponRewardBackdrop",
                "_autoControlButton",
                "_skipControlButton",
                "_speedControlButton",
                "_speedControlLabel",
                "_speedToast",
                "_hideControlButton",
            };
            foreach (string propertyName in requiredReferences)
            {
                var property = serializedView.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                    throw new InvalidOperationException(
                        $"[ConversationUIBuilder] 必須参照 {propertyName} が未設定です。"
                    );
            }
        }

        private static void ValidateNoMissingScripts(GameObject root)
        {
            foreach (var target in root.GetComponentsInChildren<Transform>(true))
            {
                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    target.gameObject
                );
                if (missingCount > 0)
                    throw new InvalidOperationException(
                        $"[ConversationUIBuilder] {target.name} にMissing Scriptが{missingCount}件あります。"
                    );
            }
        }

        private static void ValidateHistoryReferences(DialogueHistoryPanel history)
        {
            var serializedHistory = new SerializedObject(history);
            string[] requiredReferences =
            {
                "_font",
                "_panel",
                "_openButton",
                "_content",
                "_scrollRect",
                "_panelGroup",
                "_latestButton",
                "_searchField",
                "_scrollIndicator",
            };
            foreach (string propertyName in requiredReferences)
            {
                var property = serializedHistory.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                    throw new InvalidOperationException(
                        $"[ConversationUIBuilder] 履歴UIの必須参照 {propertyName} が未設定です。"
                    );
            }
        }

        private static Image CreateRewardImage(
            string name,
            Transform parent,
            Color color,
            Vector2 size
        )
        {
            var target = CreateUI(name, parent);
            var image = target.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            SetAnchored(
                target,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 220f),
                size
            );
            return image;
        }

        private static Button CreateDockButton(
            Transform parent,
            TMP_FontAsset font,
            string label,
            string shortcut,
            float x,
            float width,
            string description
        )
        {
            var target = CreateUI(label + shortcut + "Button", parent);
            var image = target.AddComponent<Image>();
            image.color = new Color(0.035f, 0.045f, 0.07f, 0.86f);
            var outline = target.AddComponent<Outline>();
            outline.effectColor = new Color(0.45f, 0.62f, 0.88f, 0.3f);
            outline.effectDistance = new Vector2(1f, -1f);
            var button = target.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            SetAnchored(
                target,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(x, 28f),
                new Vector2(width, 36f)
            );
            var text = CreateText(
                "Label",
                target.transform,
                font,
                label,
                16f,
                new Color(0.82f, 0.88f, 0.96f, 0.9f),
                TextAlignmentOptions.Center
            );
            text.GetComponent<TMP_Text>().textWrappingMode = TextWrappingModes.NoWrap;
            SetStretch(text, new Vector2(0.08f, 0f), new Vector2(0.72f, 1f));

            var keycap = CreateUI("ShortcutKey", target.transform);
            keycap.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            SetAnchored(
                keycap,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-16f, 0f),
                new Vector2(22f, 22f)
            );
            var keyLabel = CreateText(
                "Label",
                keycap.transform,
                font,
                shortcut,
                14f,
                new Color(0.88f, 0.93f, 1f, 0.94f),
                TextAlignmentOptions.Center
            );
            SetStretch(keyLabel, Vector2.zero, Vector2.one);
            var accent = CreateUI("ActiveAccent", target.transform);
            var accentImage = accent.AddComponent<Image>();
            accentImage.color = new Color(0.38f, 0.78f, 1f, 0.96f);
            accentImage.raycastTarget = false;
            SetAnchored(
                accent,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(width - 4f, 2f)
            );
            accent.SetActive(false);
            target
                .AddComponent<ConversationControlButton>()
                .Configure(image, keycap.GetComponent<RectTransform>(), accentImage, description);
            return button;
        }

        private static GameObject CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string text,
            float size,
            Color color,
            TextAlignmentOptions align
        )
        {
            var go = CreateUI(name, parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
                t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            return go;
        }

        /// <summary>アンカー点固定(サイズ固定)配置。</summary>
        private static void SetAnchored(
            GameObject go,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size
        )
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
        }

        /// <summary>親矩形に対する割合ストレッチ配置(余白0)。</summary>
        private static void SetStretch(GameObject go, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
