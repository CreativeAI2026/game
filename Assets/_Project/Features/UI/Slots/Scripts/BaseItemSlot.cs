using System;
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
            if (HoverView != null)
                HoverView.AcquireLock();
        }

        public virtual void Deselect()
        {
            _isSlotSelected = false;
            if (FrameView != null)
                FrameView.SetSelected(false);
            RefreshSelectionVisuals();
            if (HoverView != null)
                HoverView.ReleaseLock();
        }

        protected virtual void RefreshSelectionVisuals() { }

        private const float ItemTransitionDuration = 0.2f;

        public void SetItemAnimated(ItemData item, int count = 1)
        {
            SetItem(item, count);
            if (IconView != null)
                IconView.PlayAppear(ItemTransitionDuration);
            if (CountBadgeView != null)
                CountBadgeView.AnimateAppear(ItemTransitionDuration);
        }

        public void ClearAnimated(Action onComplete = null)
        {
            KillItemTransition();
            _item = null;
            _count = 0;

            if (CountBadgeView != null)
                CountBadgeView.PlayHide(ItemTransitionDuration);
            if (
                IconView == null
                || !IconView.PlayHide(
                    ItemTransitionDuration,
                    () =>
                    {
                        Clear();
                        onComplete?.Invoke();
                    }
                )
            )
            {
                Clear();
                onComplete?.Invoke();
            }
        }

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
