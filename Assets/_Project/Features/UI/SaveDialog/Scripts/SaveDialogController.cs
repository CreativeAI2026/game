using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.SaveDialog
{
    public class SaveDialogController : UIPanelStub
    {
        [SerializeField]
        private Button _yesButton;

        [SerializeField]
        private Button _noButton;

        protected override void Awake()
        {
            base.Awake();
            if (_yesButton != null)
                _yesButton.onClick.AddListener(OnYes);
            if (_noButton != null)
                _noButton.onClick.AddListener(OnNo);
        }

        private void OnDestroy()
        {
            if (_yesButton != null)
                _yesButton.onClick.RemoveListener(OnYes);
            if (_noButton != null)
                _noButton.onClick.RemoveListener(OnNo);
        }

        private void OnYes()
        {
            SaveService.Save();
            Close();
        }

        private void OnNo()
        {
            Close();
        }
    }
}
