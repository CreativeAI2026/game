using System.Collections;

namespace CreativeAI.Core
{
    public interface ILoadingOverlay
    {
        IEnumerator ShowCoroutine(float duration);
        IEnumerator HideCoroutine(float duration);
        void SetProgress(float progress01);
    }
}
