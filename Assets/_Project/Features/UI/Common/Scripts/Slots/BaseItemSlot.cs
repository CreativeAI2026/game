using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    public abstract partial class BaseItemSlot : MonoBehaviour
    {
        protected ItemData _item;
        protected int _count;
        protected bool _isSlotSelected;

        protected virtual SlotIconView IconView => null;
        protected virtual SlotCountBadgeView CountBadgeView => null;
        protected virtual SlotHoverView HoverView => null;
        protected virtual SlotFrameView FrameView => null;

        public ItemData Item => _item;
        public int Count => _count;

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
            FrameView?.SetSelected(false);
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
            IconView?.SetIcon(_item);
            CountBadgeView?.SetCount(_item, _count);
            FrameView?.SetContent(_item, _count);
        }

        public virtual void Select()
        {
            _isSlotSelected = true;
            FrameView?.SetSelected(true);
            RefreshSelectionVisuals();
            HoverView?.AcquireLock();
        }

        public virtual void Deselect()
        {
            _isSlotSelected = false;
            FrameView?.SetSelected(false);
            RefreshSelectionVisuals();
            HoverView?.ReleaseLock();
        }

        protected virtual void RefreshSelectionVisuals() { }
    }
}
