using UnityEngine;
using UnityEngine.UI;
using CreativeAI.Core.SceneManagement;

namespace CreativeAI.UI.TitleUI
{
    public class TitleUIController : MonoBehaviour
    {
        [SerializeField] private Button _tapToStartButton;
        [SerializeField] private string _nextSceneName = SceneNames.FieldArea01;

        private void Awake()
        {
            if (_tapToStartButton == null)
            {
                Debug.LogError("[TitleUIController] _tapToStartButton is not assigned.");
                return;
            }
            _tapToStartButton.onClick.AddListener(OnTapToStart);
        }

        private void OnDestroy()
        {
            if (_tapToStartButton != null)
            {
                _tapToStartButton.onClick.RemoveListener(OnTapToStart);
            }
        }

        private void OnTapToStart()
        {
            if (SceneController.Instance == null)
            {
                Debug.LogError("[TitleUIController] SceneController.Instance is null. Did you launch from 00_Boot?");
                return;
            }
            _tapToStartButton.interactable = false;
            SceneController.Instance.LoadScene(_nextSceneName);
        }
    }
}
