using CreativeAI.UI.ConversationUI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        private const string ConversationArtPath = "Assets/_Project/Art/UI/Conversation/";
        private const string WindowPng = ConversationArtPath + "ConversationWindow.png";
        private const string ContinueButtonPng =
            ConversationArtPath + "ConversationContinueButton.png";
        private const string ChoiceButtonPng = ConversationArtPath + "ConversationChoiceButton.png";
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

            // --- 立ち絵 ---
            var portrait = CreateUI("Portrait", root.transform);
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

            // --- ウィンドウ ---
            var window = CreateUI("Window", root.transform);
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
                new Vector2(0, 64),
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

            // 本文(明るい本体)
            var bodyText = CreateText(
                "BodyText",
                window.transform,
                font,
                "ここに会話テキストが表示されます。クリックまたはスペース/Enter/Zキーで送れます。",
                34,
                new Color(1f, 1f, 1f, 1f),
                TextAlignmentOptions.TopLeft
            );
            SetStretch(bodyText, new Vector2(0.12f, 0.16f), new Vector2(0.95f, 0.72f));

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
                root.transform,
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
            so.FindProperty("_nameText").objectReferenceValue = nameText.GetComponent<TMP_Text>();
            so.FindProperty("_bodyText").objectReferenceValue = bodyText.GetComponent<TMP_Text>();
            so.FindProperty("_nextIndicator").objectReferenceValue = nextIndicator;
            so.FindProperty("_autoModeIndicator").objectReferenceValue =
                autoModeIndicator.GetComponent<TMP_Text>();
            so.FindProperty("_historyPanel").objectReferenceValue =
                root.GetComponent<DialogueHistoryPanel>();
            so.FindProperty("_choiceContainer").objectReferenceValue =
                choiceContainer.GetComponent<RectTransform>();
            so.FindProperty("_choiceButtonTemplate").objectReferenceValue = button;
            so.FindProperty("_defaultPortrait").objectReferenceValue = portraitSprite;

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

            // --- Prefab 保存 ---
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
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
