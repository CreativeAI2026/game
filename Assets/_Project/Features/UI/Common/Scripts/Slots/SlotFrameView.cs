using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public enum SlotFrameRole
    {
        Item,
        ItemSet,
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

        [Header("Slot Type")]
        [SerializeField]
        private SlotFrameRole _role = SlotFrameRole.Item;

        private bool _selected;
        private bool _hasVisibleCount;
        private bool _hasWarnedMissingFrame;
        private bool _hasWarnedMissingSprite;

        public SlotFrameRole Role => _role;
        public bool IsSelected => _selected;
        public bool HasVisibleCount => _hasVisibleCount;
        public RectTransform FrameRect => _frame != null ? _frame.rectTransform : null;
        public Sprite CurrentSprite => _frame != null ? _frame.sprite : null;
        public bool HasRequiredReferences =>
            _frame != null
            && _normalSprite != null
            && _selectedSprite != null
            && _itemSetSprite != null
            && _itemWithCountSprite != null;

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
            if (_role == SlotFrameRole.ItemSet)
                return _itemSetSprite;
            if (_selected)
                return _selectedSprite;
            if (_hasVisibleCount)
                return _itemWithCountSprite;
            return _normalSprite;
        }

        private void ApplyVisual()
        {
            if (!ResolveFrame())
                return;

            Sprite sprite = ResolveSprite();
            if (sprite == null)
            {
                WarnMissingSpriteOnce();
                return;
            }

            _frame.sprite = sprite;
            _frame.type = Image.Type.Simple;
            _frame.color = Color.white;
        }

        private bool ResolveFrame()
        {
            if (_frame != null)
                return true;

            if (!_hasWarnedMissingFrame)
            {
                _hasWarnedMissingFrame = true;
                Debug.LogWarning(
                    $"{nameof(SlotFrameView)} '{name}' にFrame Imageがないため、枠表示をスキップします。Prefab上で設定してください。",
                    this
                );
            }

            return false;
        }

        private void WarnMissingSpriteOnce()
        {
            if (_hasWarnedMissingSprite)
                return;

            _hasWarnedMissingSprite = true;
            Debug.LogWarning(
                $"{nameof(SlotFrameView)} '{name}' の状態Spriteが不足しています。PrefabのNormal / Selected / ItemSet / ItemWithCountを設定してください。",
                this
            );
        }

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _frame ??= GetComponent<Image>();
            if (_frame != null)
                return;

            var frameTransform = transform.Find("VisualRoot/Frame") ?? transform.Find("Frame");
            _frame = frameTransform != null ? frameTransform.GetComponent<Image>() : null;
        }
#endif
    }
}
