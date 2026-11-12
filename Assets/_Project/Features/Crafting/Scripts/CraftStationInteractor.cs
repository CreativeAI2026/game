using CreativeAI.Core;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 合成装置に近づくと「合成する」を出し、キーで合成画面を開く。近接判定とプロンプトは <see cref="ProximityInteractor"/> が受け持つ。
    /// 画面は <see cref="CraftUiRequestService"/> 越しに常駐UIへ頼む(ワールド側から UI アセンブリを参照できないため)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CraftStationInteractor : ProximityInteractor
    {
        [Tooltip("プロンプトに出す文言。")]
        [SerializeField]
        private string _label = "合成する";

        protected override bool HasTarget => true;

        protected override string ActionLabel => _label;

        protected override void Perform() => CraftUiRequestService.Request();
    }
}
