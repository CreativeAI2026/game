using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 合成画面を開く依頼の静的サービス。ワールド側(Gameplay)は UI を参照できないので、
    /// ワールド側は依頼するだけ、UI 側は購読して開くだけにする(<see cref="InteractPromptService"/> と同じ考え方)。
    /// </summary>
    public static class CraftUIRequestService
    {
        /// <summary>合成画面を開く依頼が出た。</summary>
        public static event System.Action Requested;

        public static void Request() => Requested?.Invoke();

        /// <summary>購読を外す。</summary>
        // Domain Reload を切った Editor で Play を繰り返したとき、前回の Play の値や購読を持ち越さないよう、Play 開始時に戻す
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetState() => Requested = null;
    }
}
