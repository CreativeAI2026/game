using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 常駐UIの出し入れ。GameObject ごと止めると購読が切れて戻れないため、CanvasGroup で透明・入力なしにして生かしたまま隠す。
    /// Canvas は UIRoot 側の入れ物が持つので、中身はこの CanvasGroup で出し入れする。
    /// </summary>
    public static class ResidentUiVisibility
    {
        /// <summary>自分の CanvasGroup を返す。無ければ付ける(Prefab に置き忘れても隠せるように)。</summary>
        public static CanvasGroup GetOrAddGroup(GameObject owner) =>
            owner.TryGetComponent(out CanvasGroup group)
                ? group
                : owner.AddComponent<CanvasGroup>();

        public static void SetShown(this CanvasGroup group, bool shown)
        {
            group.alpha = shown ? 1f : 0f;
            group.interactable = shown;
            group.blocksRaycasts = shown;
        }

        public static bool IsShown(this CanvasGroup group) => group.alpha > 0f;
    }
}
