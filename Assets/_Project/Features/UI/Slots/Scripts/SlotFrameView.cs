using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public enum SlotFrameRole
    {
        Item,
        ItemSet,
        Custom,
    }

    public class SlotFrameView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private Image _frame;

        [Header("Sprites")]
        [SerializeField]
        private Sprite _normalSprite;

        [SerializeField]
        private Sprite _selectedSprite;

        [SerializeField]
        private Sprite _itemSetSprite;

        [SerializeField]
        private Sprite _itemWithCountSprite;

        [SerializeField]
        private Sprite _itemSetSelectedSprite;

        [SerializeField]
        private Sprite _itemWithCountSelectedSprite;

        [SerializeField]
        private Sprite _customSelectedSprite;

        [Header("Slot Type")]
        [SerializeField]
        private SlotFrameRole _role = SlotFrameRole.Item;

        private bool _selected;
        private bool _hasVisibleCount;

        public bool HasVisibleCount => _hasVisibleCount;
        public RectTransform FrameRect => _frame != null ? _frame.rectTransform : null;
        public Sprite CurrentSprite => _frame != null ? _frame.sprite : null;

        private void OnEnable() => ApplyVisual();

        public void SetSelected(bool selected)
        {
            _selected = selected;
            ApplyVisual();
        }

        public void SetContent(ItemData item, int count)
        {
            _hasVisibleCount = SlotCountBadgeView.ShouldShowCount(item, count);
            ApplyVisual();
        }

        public void SetRole(SlotFrameRole role)
        {
            _role = role;
            ApplyVisual();
        }

        public Sprite ResolveSprite()
        {
            if (_role == SlotFrameRole.Custom)
                return _selected && _customSelectedSprite != null
                    ? _customSelectedSprite
                    : _normalSprite;
            if (_role == SlotFrameRole.ItemSet)
                return _selected && _itemSetSelectedSprite != null
                    ? _itemSetSelectedSprite
                    : _itemSetSprite;
            if (_selected && _hasVisibleCount && _itemWithCountSelectedSprite != null)
                return _itemWithCountSelectedSprite;
            if (_selected)
                return _selectedSprite;
            if (_hasVisibleCount)
                return _itemWithCountSprite;
            return _normalSprite;
        }

        private void ApplyVisual()
        {
            // その役割・状態の画像が無ければ、今の見た目のままにする
            Sprite sprite = ResolveSprite();
            if (sprite == null)
                return;

            _frame.sprite = sprite;
            _frame.type = Image.Type.Simple;
            _frame.color = Color.white;
        }

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            if (_frame == null)
                TryGetComponent(out _frame);
            if (_frame != null)
                return;

            var frameTransform = transform.Find("VisualRoot/Frame");
            if (frameTransform == null)
                frameTransform = transform.Find("Frame");
            _frame = frameTransform != null ? frameTransform.GetComponent<Image>() : null;
        }
#endif
    }
}
