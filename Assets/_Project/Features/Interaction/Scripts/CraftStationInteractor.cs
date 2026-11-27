using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 合成装置に近づくと「合成する」を出し、キーで合成画面を開く。近接判定とプロンプトは <see cref="ProximityInteractor"/> が受け持つ。
    /// ワールド側から UI アセンブリを参照できないので、<see cref="Requested"/> を鳴らすだけにし、常駐の UIManager が購読して開く。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CraftStationInteractor : ProximityInteractor
    {
        [Tooltip("プロンプトに出す文言。")]
        [SerializeField]
        private string _label = "合成する";

        /// <summary>どれかの合成装置で「合成する」が押されたとき。UIManager が購読して合成画面を開く。</summary>
        public static event Action Requested;

        protected override bool HasTarget => true;

        protected override string ActionLabel => _label;

        protected override void Perform() => Requested?.Invoke();

        // Domain Reload を切った Editor で Play を繰り返したとき、前回の Play の購読を持ち越さないよう、Play 開始時に戻す
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetState() => Requested = null;
    }
}
