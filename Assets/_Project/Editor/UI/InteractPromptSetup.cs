using CreativeAI.UI.InteractPrompt;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.EditorTools.UI
{
    /// <summary>
    /// 操作プロンプト(「[E] 扉を開ける」)を <see cref="CreativeAI.UI.UIRoot"/> Prefab の子として注入するツール。
    /// 仕様§6のとおり UIRoot が UI レイヤーを束ねるため、独立 Prefab ではなく UIRoot.prefab に同梱する
    /// (常駐・単一化・DontDestroyOnLoad は UIRoot が担う = Title/config への追加配線は不要)。
    /// 排他パネルではないので UiRouter.UiId には足さない。
    /// 冪等: 既に "InteractPrompt" 子が在れば作り直す。
    /// </summary>
    public static class InteractPromptSetup
    {
        private const string UIRootPath = "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab";
        private const string PromptName = "InteractPrompt";

        [MenuItem("Tools/CreativeAI/UI/Inject Interact Prompt Into UIRoot")]
        public static void InjectIntoUIRoot()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(UIRootPath) == null)
            {
                Debug.LogError($"[InteractPrompt] UIRoot Prefab が見つかりません: {UIRootPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(UIRootPath);
            try
            {
                var existing = root.transform.Find(PromptName);
                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);

                Build(root);
                PrefabUtility.SaveAsPrefabAsset(root, UIRootPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[InteractPrompt] UIRoot.prefab に '{PromptName}' を注入しました({UIRootPath})。\n"
                    + "位置は画面下中央(下から 180px)。パネル表示中・会話中・戦闘中は自動で隠れる。"
            );
        }

        private static void Build(GameObject uiRoot)
        {
            // ルート = 自前 Canvas。クリックを受けないので GraphicRaycaster は付けない。
            var prompt = new GameObject(
                PromptName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler)
            );
            prompt.transform.SetParent(uiRoot.transform, false);

            var canvas = prompt.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 55; // QuickFoodBar(50)の上・開くパネルの下
            canvas.enabled = false; // 何も出ていない状態から始める

            var scaler = prompt.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 帯 = 下中央。文字量で伸びるように ContentSizeFitter を付ける。
            var body = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(Image),
                typeof(ContentSizeFitter),
                typeof(HorizontalLayoutGroup)
            );
            body.transform.SetParent(prompt.transform, false);
            var rt = body.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 180f); // 下端の QuickFoodBar と重ならない高さ

            var bg = body.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            var layout = body.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 12, 12);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = body.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(body.transform, false);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.text = "[E] 扉を開ける";
            text.fontSize = 34f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;

            var view = prompt.AddComponent<InteractPromptView>();
            var so = new SerializedObject(view);
            so.FindProperty("_label").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
