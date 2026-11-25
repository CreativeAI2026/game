using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// ドラッグ中のアイテムの絵をポインタに追従させる。押下が離れたらドラッグを終える。
    /// </summary>
    internal sealed class ItemDragGhostFollower : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (
                !UIInputActionMap.IsClickPressed()
                || !UIInputActionMap.TryReadPointerPosition(out Vector2 position)
            )
            {
                ItemSlotDragContext.Clear();
                return;
            }

            ItemSlotDragContext.MoveToScreenPosition(position);
        }
    }
}
