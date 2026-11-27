using System;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    [RequireComponent(typeof(Button), typeof(Image))]
    public class EquipmentSlot : BaseItemSlot, IPointerDownHandler, IPointerClickHandler
    {
        private const float EmptyIconAlpha = 0f;

        [SerializeField]
        [Tooltip("空のときに出すもの")]
        private GameObject _emptyObject;

        private bool _acceptPointerClick;
        private ItemStack _stack;

        public event Action<EquipmentSlot> Clicked;

        public ItemStack Stack => _stack;

        public void Init()
        {
            if (IconView != null)
                IconView.SetEmptyAlpha(EmptyIconAlpha);
            if (CountBadgeView != null)
            {
                RectTransform positionReference = null;
                if (IconView != null)
                    positionReference =
                        IconView.FitRect != null ? IconView.FitRect : IconView.IconRect;
                CountBadgeView.SetPositionReference(positionReference);
            }
            Refresh();
        }

        public override void SetItem(ItemData item, int count = 1)
        {
            _stack = null;
            base.SetItem(item, count);
        }

        public void SetStack(ItemStack stack)
        {
            _stack = stack;
            base.SetItem(stack?.Data, stack?.Count ?? 0);
        }

        protected override void Refresh()
        {
            if (IconView != null)
                IconView.SetEmptyAlpha(EmptyIconAlpha);
            base.Refresh();
            if (_emptyObject != null)
                _emptyObject.SetActive(_item == null || _item.icon == null);
        }

        public override void Clear()
        {
            _stack = null;
            base.Clear();
            if (_emptyObject != null)
                _emptyObject.SetActive(true);
        }

        public void UpdateCount()
        {
            SetCount(_stack?.Count ?? FindInventoryCount(_item));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _acceptPointerClick = eventData.button == PointerEventData.InputButton.Left;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            bool accepted = _acceptPointerClick;
            _acceptPointerClick = false;

            if (!accepted || eventData.button != PointerEventData.InputButton.Left)
                return;

            if (eventData.clickCount >= 2)
                return;

            Clicked?.Invoke(this);
        }

        private static int FindInventoryCount(ItemData item)
        {
            if (item == null)
                return 0;

            InventoryManager inventory = InventoryManager.Instance;
            var stack =
                inventory != null ? inventory.GetAllItems().Find(s => s.Data == item) : null;
            return stack?.Count ?? 1;
        }

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            AutoAssignSlotViews();
            if (_emptyObject == null)
            {
                var empty = transform.Find("VisualRoot/EmptyText");
                _emptyObject = empty != null ? empty.gameObject : null;
            }
        }
#endif
    }
}
