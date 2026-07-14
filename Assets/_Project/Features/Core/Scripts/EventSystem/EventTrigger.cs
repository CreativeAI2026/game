using UnityEngine;

namespace CreativeAI.Core.EventSystem
{
    /// <summary>
    /// シーン上のトリガーに配置する非常駐コンポーネント。プレイヤー侵入を検知し、
    /// 条件(progress / flag をすべて AND)を満たせば EventPlayer に発火を託すルーター役。
    /// 自身はイベントの中身を再生しない。documents/Specification.md §4, EventImplementation.md 参照。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class EventTrigger : MonoBehaviour
    {
        [SerializeField]
        private EventDefinition _event;

        [SerializeField]
        private string _playerTag = "Player";

        // IEventPlayer を実装する MonoBehaviour を割り当てる(EventPlayer は次段で実装)。
        [SerializeField]
        private MonoBehaviour _eventPlayer;

        private IEventPlayer Player => _eventPlayer as IEventPlayer;

        private void OnTriggerEnter(Collider other)
        {
            if (_event == null || !other.CompareTag(_playerTag))
                return;

            // Battle 中は新規イベントを発火しない(多重発火防止・Specification.md §6)。
            var mode = GameModeManager.Instance;
            if (mode != null && mode.CurrentMode == GameMode.Battle)
                return;

            var progress = ProgressManager.Instance;
            if (progress == null)
            {
                Debug.LogWarning(
                    $"[EventTrigger] '{name}': ProgressManager.Instance が無いため条件評価不可 (event={_event.Id})."
                );
                return;
            }

            if (!_event.ConditionsMet(progress.Progress, progress.GetFlag))
                return;

            var player = Player;
            if (player == null)
            {
                Debug.LogWarning(
                    $"[EventTrigger] '{name}': IEventPlayer 未割り当てのため発火をスキップ (event={_event.Id})."
                );
                return;
            }

            player.Play(_event);
        }
    }
}
