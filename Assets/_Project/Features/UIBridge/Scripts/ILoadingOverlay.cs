using System.Collections;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 遷移中に出す暗転・読み込み表示の窓口。実体は UI 側(LoadingOverlayView)で、
    /// Gameplay は UI を参照できないためこの契約を挟む。SceneLoader が子から探して使う。
    /// </summary>
    public interface ILoadingOverlay
    {
        IEnumerator ShowCoroutine(float duration);
        IEnumerator HideCoroutine(float duration);
        void SetProgress(float progress01);
    }
}
