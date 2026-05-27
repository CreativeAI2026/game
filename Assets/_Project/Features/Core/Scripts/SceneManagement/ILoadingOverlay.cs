using System.Collections;

namespace CreativeAI.Core.SceneManagement
{
    public interface ILoadingOverlay
    {
        IEnumerator ShowCoroutine(float duration);
        IEnumerator HideCoroutine(float duration);
        void SetProgress(float progress01);
    }
}
