using UnityEngine;

namespace CreativeAI.UI.InventoryUI
{
    public partial class ItemSlot
    {
        [SerializeField]
        private RectTransform _selectedFrameRect;

        public override void Select()
        {
            base.Select();
        }

        public override void Deselect()
        {
            base.Deselect();
        }

        protected override void RefreshSelectionVisuals()
        {
            ConfigureSelectedFrame();
        }

        private void ConfigureSelectedFrame()
        {
            _selectedFrameRect ??=
                transform.Find("VisualRoot/SelectedFrame") as RectTransform
                ?? transform.Find("SelectedFrame") as RectTransform;
            if (_selectedFrameRect == null)
                return;

            DisableGraphicRaycasts(_selectedFrameRect);
            _selectedFrameRect.gameObject.SetActive(_isSlotSelected);
        }
    }
}
