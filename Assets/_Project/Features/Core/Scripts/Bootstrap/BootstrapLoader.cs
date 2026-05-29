using CreativeAI.Core.SceneManagement;
using UnityEngine;

namespace CreativeAI.Core.Bootstrap
{
    public class BootstrapLoader : MonoBehaviour
    {
        [SerializeField]
        private string _firstSceneName = SceneNames.Title;

        private void Start()
        {
            if (SceneController.Instance == null)
            {
                Debug.LogError(
                    "[BootstrapLoader] SceneController.Instance is null. Place SceneController in 00_Boot before Bootstrap runs."
                );
                return;
            }
            SceneController.Instance.LoadScene(_firstSceneName);
        }
    }
}
