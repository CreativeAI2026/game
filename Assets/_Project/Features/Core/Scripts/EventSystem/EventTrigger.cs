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

        [Tooltip(
            "battle ステップを含むイベント用。トリガー位置に出す敵 Prefab(Project の Prefab)。"
        )]
        [SerializeField]
        private GameObject _enemy;

        [Tooltip("敵の出現位置(任意)。未設定ならこのトリガーの位置・向きに出す。")]
        [SerializeField]
        private Transform _spawnPoint;

        // IEventPlayer を実装する MonoBehaviour を割り当てる(任意)。EventPlayer は常駐化したので
        // 未割当なら EventPlayerService.Current にフォールバックする(per-field 配線は不要)。
        [SerializeField]
        private MonoBehaviour _eventPlayer;

        // 明示配線があればそれを、無ければ常駐 EventPlayer(seam)を使う。破棄済み参照は Unity null で弾く。
        private IEventPlayer Player =>
            (_eventPlayer != null ? _eventPlayer as IEventPlayer : null)
            ?? EventPlayerService.Current;

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

            // hasItem 条件用の所持判定。Inventory は Gameplay 側にあり Core から直接触れないため
            // ItemGiverService seam 経由(未登録なら所持なし扱い)。giveItem と同じ経路。
            var giver = ItemGiverService.Current;
            System.Func<string, bool> hasItem = giver != null ? giver.HasImportantItem : null;
            if (!_event.ConditionsMet(progress.Progress, progress.GetFlag, hasItem))
                return;

            var player = Player;
            if (player == null)
            {
                Debug.LogWarning(
                    $"[EventTrigger] '{name}': IEventPlayer が見つからず発火をスキップ (event={_event.Id})。"
                        + " 常駐 EventPlayer(SessionBootstrap)が未生成か、_eventPlayer が未割当です。"
                );
                return;
            }

            var spawn = _spawnPoint != null ? _spawnPoint : transform;
            player.Play(_event, new BattleSetup(_enemy, spawn.position, spawn.rotation));
        }
    }
}
