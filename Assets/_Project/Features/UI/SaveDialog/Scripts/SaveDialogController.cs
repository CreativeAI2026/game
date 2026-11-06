using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI
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

        private void OnEnable()
        {
            if (_yesButton != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_yesButton.gameObject);
            }
        }

        private void Update()
        {
            if (
                EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == null
                && _yesButton != null
            )
            {
                EventSystem.current.SetSelectedGameObject(_yesButton.gameObject);
            }
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
