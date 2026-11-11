using CreativeAI.Core;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 近づくと「調べる」を出し、キーでイベントを発火する(events.json の activation が interact のイベント用)。
    /// 近接判定とプロンプトは <see cref="ProximityInteractor"/> が受け持ち、発火条件は <see cref="EventTrigger"/> と同じ
    /// <see cref="EventAvailability"/> で見る。1回きりのイベントは、見終えるとプロンプトごと出なくなる。
    /// 戦闘(battle)を含むイベントは取り込み時に弾かれるので、敵の配線は持たない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EventInteractor : ProximityInteractor
    {
        [SerializeField]
        private EventDefinition _event;

        [Tooltip("プロンプトに出す文言。")]
        [SerializeField]
        private string _label = "調べる";

        protected override bool HasTarget => EventAvailability.IsMet(_event);

        protected override string ActionLabel => _label;

        private void OnValidate()
        {
            if (_event != null && _event.Activation != EventActivation.Interact)
                Debug.LogWarning(
                    $"[EventInteractor] '{name}': イベント '{_event.Id}' は activation が interact ではありません。"
                        + " 入っただけで発火する EventTrigger に付け替えるか、events.json に activation を書いてください。",
                    this
                );
        }

        protected override void Perform()
        {
            var player = EventPlayerService.Current;
            if (player == null)
            {
                Debug.LogWarning(
                    $"[EventInteractor] '{name}': IEventPlayer が見つからず発火をスキップ (event={_event.Id})。"
                        + " 常駐 EventPlayer(GameSession)が未生成です。"
                );
                return;
            }
            player.Play(_event);
        }
    }
}
