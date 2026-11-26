using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    public abstract class BaseItemSlot : MonoBehaviour
    {
        protected ItemData _item;
        protected int _count;
        protected bool _isSlotSelected;

        protected virtual SlotIconView IconView => null;
        protected virtual SlotCountBadgeView CountBadgeView => null;
        protected virtual HoverScaleOnPointer Hover => null;
        protected virtual SlotFrameView FrameView => null;

        protected virtual void Awake()
        {
            Refresh();
        }

        public virtual void SetItem(ItemData item, int count = 1)
        {
            KillItemTransition();
            _item = item;
            _count = item == null ? 0 : Mathf.Max(0, count);
            Refresh();
            ResetItemVisuals();
        }

        public virtual void Clear()
        {
            KillItemTransition();
            _item = null;
            _count = 0;
            _isSlotSelected = false;
            Refresh();
            if (FrameView != null)
                FrameView.SetSelected(false);
            RefreshSelectionVisuals();
            ResetItemVisuals();
        }

        protected void SetCount(int count)
        {
            _count = _item == null ? 0 : Mathf.Max(0, count);
            Refresh();
        }

        protected virtual void Refresh()
        {
            if (IconView != null)
                IconView.SetIcon(_item);
            if (CountBadgeView != null)
                CountBadgeView.SetCount(_item, _count);
            if (FrameView != null)
                FrameView.SetContent(_item, _count);
        }

        public virtual void Select()
        {
            _isSlotSelected = true;
            if (FrameView != null)
                FrameView.SetSelected(true);
            RefreshSelectionVisuals();
            if (Hover != null)
                Hover.AcquireLock();
        }

        public virtual void Deselect()
        {
            _isSlotSelected = false;
            if (FrameView != null)
                FrameView.SetSelected(false);
            RefreshSelectionVisuals();
            if (Hover != null && Hover.IsLocked())
                Hover.ReleaseLock();
        }

        protected virtual void RefreshSelectionVisuals() { }

        private void KillItemTransition()
        {
            if (IconView != null)
                IconView.KillTween();
            if (CountBadgeView != null)
                CountBadgeView.KillTween();
        }

        private void ResetItemVisuals()
        {
            if (IconView != null)
                IconView.ResetVisual();
            if (CountBadgeView != null)
                CountBadgeView.ResetVisual();
        }
    }
}
