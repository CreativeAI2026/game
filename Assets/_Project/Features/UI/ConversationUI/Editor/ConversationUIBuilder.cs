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
    /// 会話UIの Prefab と確認用シーン Field_Area05 を一括生成する Editor ツール。
    /// 立ち絵/ウィンドウ画像を Sprite として import し、ConversationView.prefab を組み立て、
    /// Area05 シーンにその実体 + プレビュー駆動役を配置する。手書き YAML を避け Unity に正しく
    /// シリアライズさせるための道具(メニューからも実行可)。
    /// </summary>
    public static class ConversationUIBuilder
    {
        private const string WindowPng = "Assets/_Project/Art/UI/Backgrounds/chat-window.png";
        private const string PortraitPng = "Assets/_Project/Art/UI/Portraits/dummy-characters.png";
        private const string FontPath =
            "Assets/_Project/Art/UI/Fonts/NotoSansJP-VariableFont_wght SDF.asset";
        private const string PrefabPath =
            "Assets/_Project/Features/UI/ConversationUI/Prefabs/ConversationView.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/Field/Field_Area05.unity";

        [MenuItem("Tools/CreativeAI/Build Conversation UI (Area05)")]
        public static void BuildAll()
        {
            ImportAsSprite(WindowPng);
            ImportAsSprite(PortraitPng);
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

        private static GameObject BuildPrefab()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var windowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WindowPng);
            var portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPng);

            // --- ルート(Canvas + 常駐コンポーネント) ---
            var root = new GameObject(
                "ConversationView",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup),
                typeof(ConversationView)
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
            // 会話ウィンドウの上に立たせる(下辺中央アンカー。ウィンドウ上辺に少しかぶせる)
            SetAnchored(
                portrait,
                new Vector2(0.5f, 0),
                new Vector2(0.5f, 0),
                new Vector2(0.5f, 0),
                new Vector2(0, 430),
                new Vector2(520, 520)
            );

            // --- ウィンドウ ---
            var window = CreateUI("Window", root.transform);
            var windowImg = window.AddComponent<Image>();
            windowImg.sprite = windowSprite;
            windowImg.preserveAspect = true;
            windowImg.raycastTarget = true; // クリックで送るための当たり判定
            // 画面下部のダイアログボックス(透過余白ぶん下へ沈めて可視枠を下寄せ)
            SetAnchored(
                window,
                new Vector2(0.5f, 0),
                new Vector2(0.5f, 0),
                new Vector2(0.5f, 0),
                new Vector2(0, -90),
                new Vector2(1100, 733)
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
            SetStretch(nameText, new Vector2(0.07f, 0.635f), new Vector2(0.36f, 0.725f));

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
            SetStretch(bodyText, new Vector2(0.075f, 0.30f), new Vector2(0.93f, 0.60f));

            // 送り待ちの点滅三角
            var nextIndicator = CreateText(
                "NextIndicator",
                window.transform,
                font,
                "▼",
                34,
                new Color(1f, 1f, 1f, 1f),
                TextAlignmentOptions.Center
            );
            SetStretch(nextIndicator, new Vector2(0.85f, 0.285f), new Vector2(0.92f, 0.35f));

            // 選択肢コンテナ(通常は非表示)
            var choiceContainer = CreateUI("ChoiceContainer", window.transform);
            var vlg = choiceContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            SetStretch(choiceContainer, new Vector2(0.28f, 0.28f), new Vector2(0.72f, 0.62f));

            // 選択肢ボタンの雛形(非active。実行時に複製)
            var choiceTemplate = CreateUI("ChoiceButtonTemplate", choiceContainer.transform);
            var btnImg = choiceTemplate.AddComponent<Image>();
            btnImg.color = new Color(0.20f, 0.22f, 0.28f, 0.96f);
            var button = choiceTemplate.AddComponent<Button>();
            button.targetGraphic = btnImg;
            var le = choiceTemplate.AddComponent<LayoutElement>();
            le.minHeight = 78;
            le.preferredHeight = 78;
            var choiceLabel = CreateText(
                "Label",
                choiceTemplate.transform,
                font,
                "選択肢",
                32,
                new Color(1f, 1f, 1f, 1f),
                TextAlignmentOptions.Center
            );
            SetStretch(choiceLabel, Vector2.zero, Vector2.one);
            choiceTemplate.SetActive(false);

            // --- ConversationView の直列フィールドを配線 ---
            var view = root.GetComponent<ConversationView>();
            var so = new SerializedObject(view);
            so.FindProperty("_root").objectReferenceValue = root.GetComponent<CanvasGroup>();
            so.FindProperty("_portrait").objectReferenceValue = portraitImg;
            so.FindProperty("_nameText").objectReferenceValue = nameText.GetComponent<TMP_Text>();
            so.FindProperty("_bodyText").objectReferenceValue = bodyText.GetComponent<TMP_Text>();
            so.FindProperty("_nextIndicator").objectReferenceValue = nextIndicator;
            so.FindProperty("_choiceContainer").objectReferenceValue =
                choiceContainer.GetComponent<RectTransform>();
            so.FindProperty("_choiceButtonTemplate").objectReferenceValue = button;
            so.FindProperty("_defaultPortrait").objectReferenceValue = portraitSprite;

            var portraits = so.FindProperty("_portraits");
            portraits.arraySize = 1;
            var e0 = portraits.GetArrayElementAtIndex(0);
            e0.FindPropertyRelative("Key").stringValue = "dummy";
            e0.FindPropertyRelative("Sprite").objectReferenceValue = portraitSprite;
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
