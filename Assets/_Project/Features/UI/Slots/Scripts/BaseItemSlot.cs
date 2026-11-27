using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// アイテムを1つ入れて見せるスロットの共通部分。アイコン・個数・枠・ホバーの見た目を持ち、
    /// 入れる・空にする・選ぶの見た目をそろえる。個数や枠を持たないスロットは、その欄を空にしておく。
    /// </summary>
    public abstract class BaseItemSlot : MonoBehaviour
    {
        [SerializeField]
        private SlotIconView _iconView;

        [SerializeField]
        private SlotCountBadgeView _countBadgeView;

        [SerializeField]
        private SlotFrameView _frameView;

        [SerializeField]
        private HoverScaleOnPointer _hover;

        protected ItemData _item;
        protected int _count;
        protected bool _isSlotSelected;

        protected SlotIconView IconView => _iconView;
        protected SlotFrameView FrameView => _frameView;
        protected HoverScaleOnPointer Hover => _hover;

        /// <summary>個数バッジ。個数を出さない設定のときは null。</summary>
        protected SlotCountBadgeView CountBadgeView => ShowsCount ? _countBadgeView : null;

        /// <summary>個数を出すか。出し分けるスロットだけ上書きする。</summary>
        protected virtual bool ShowsCount => true;

        /// <summary>個数を出さない設定でも触る(隠すなど)ための、個数バッジそのもの。</summary>
        protected SlotCountBadgeView CountBadge => _countBadgeView;

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

#if UNITY_EDITOR
        /// <summary>子から見た目の部品を探して欄を埋める。</summary>
        protected void AutoAssignSlotViews()
        {
            if (_iconView == null)
                _iconView = GetComponentInChildren<SlotIconView>(true);
            if (_countBadgeView == null)
                _countBadgeView = GetComponentInChildren<SlotCountBadgeView>(true);
            if (_frameView == null)
                _frameView = GetComponentInChildren<SlotFrameView>(true);
            if (_hover == null)
                _hover = GetComponentInChildren<HoverScaleOnPointer>(true);
        }
#endif

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
