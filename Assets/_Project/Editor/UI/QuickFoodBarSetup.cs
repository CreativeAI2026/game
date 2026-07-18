using CreativeAI.UI;
using CreativeAI.UI.QuickFoodBar;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.EditorTools.UI
{
    /// <summary>
    /// 即時食材使用UI(常駐)を <see cref="UIRoot"/> Prefab の子として注入するツール。
    /// 仕様§6のとおり UIRoot が UI レイヤーを束ねるため、独立 Prefab ではなく UIRoot.prefab に同梱する
    /// (常駐・単一化・DontDestroyOnLoad は UIRoot が担う=Title/config への追加配線は不要)。
    /// 冪等: 既に "QuickFoodBar" 子が在れば作り直す。スロットは既存 EquipmentSlot Prefab を流用。
    /// </summary>
    public static class QuickFoodBarSetup
    {
        private const string UIRootPath = "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab";
        private const string SlotPrefabPath =
            "Assets/_Project/Features/UI/CharacterUI/Prefabs/EquipmentSlot.prefab";
        private const string BarName = "QuickFoodBar";
        private const int SlotCount = 3;

        [MenuItem("Tools/CreativeAI/UI/Inject Quick Food Bar Into UIRoot")]
        public static void InjectIntoUIRoot()
        {
            var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
            if (slotPrefab == null || slotPrefab.GetComponent<EquipmentSlot>() == null)
            {
                Debug.LogError(
                    $"[QuickFoodBar] EquipmentSlot Prefab が見つかりません: {SlotPrefabPath}"
                );
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(UIRootPath) == null)
            {
                Debug.LogError($"[QuickFoodBar] UIRoot Prefab が見つかりません: {UIRootPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(UIRootPath);
            try
            {
                // 冪等: 既存の QuickFoodBar 子を除去してから作り直す。
                var existing = root.transform.Find(BarName);
                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);

                BuildBar(root, slotPrefab);

                PrefabUtility.SaveAsPrefabAsset(root, UIRootPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[QuickFoodBar] UIRoot.prefab に '{BarName}' を注入しました({UIRootPath})。\n"
                    + "UIRoot は Title で既に常駐生成されるため、Title/config への追加配線は不要。\n"
                    + "見た目(位置=左下 (40,40) / scale=0.7 / 間隔 / sortingOrder=50)は UIRoot.prefab を開いて調整可。パネル/会話中は自動で隠れる。"
            );
        }

        private static void BuildBar(GameObject uiRoot, GameObject slotPrefab)
        {
            // ルート = 自前 Canvas(常時表示。モードで出し分けない)。UIRoot の子として同梱。
            var bar = new GameObject(
                BarName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );
            bar.transform.SetParent(uiRoot.transform, false);

            var canvas = bar.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50; // HUD の上・開くパネルの下あたり(必要なら調整)
            var scaler = bar.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // バー本体 = 下中央に横並び。
            var barBody = new GameObject(
                "Bar",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(ContentSizeFitter)
            );
            barBody.transform.SetParent(bar.transform, false);
            var bodyRt = barBody.GetComponent<RectTransform>();
            bodyRt.anchorMin = bodyRt.anchorMax = new Vector2(0f, 0f); // 左下アンカー
            bodyRt.pivot = new Vector2(0f, 0f);
            bodyRt.anchoredPosition = new Vector2(40f, 40f); // 左下からの余白
            bodyRt.localScale = new Vector3(0.7f, 0.7f, 1f); // 少し小さめ(左下ピボットなので位置は保たれる)
            var layout = barBody.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = barBody.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // スロット3つ(既存 EquipmentSlot を流用。プレハブ内なのでプレーンコピーで持つ)。
            var slots = new EquipmentSlot[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                var slotGo = (GameObject)Object.Instantiate(slotPrefab, barBody.transform);
                slotGo.name = $"QuickFoodSlot{i + 1}";
                slots[i] = slotGo.GetComponent<EquipmentSlot>();
            }

            // コントローラ + 参照配線(private [SerializeField] _slots は SerializedObject 経由)。
            var controller = bar.AddComponent<QuickFoodBarController>();
            var so = new SerializedObject(controller);
            var slotsProp = so.FindProperty("_slots");
            slotsProp.arraySize = SlotCount;
            for (int i = 0; i < SlotCount; i++)
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
