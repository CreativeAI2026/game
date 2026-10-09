using UnityEngine;

namespace CreativeAI.Core
{
    /// <summary>
    /// シーン上のトリガーに配置する非常駐コンポーネント。プレイヤー侵入を検知し、
    /// 条件(progress / flag / hasItem をすべて AND)を満たせば EventPlayer に発火を託すルーター役。
    /// 自身はイベントの中身を再生しない。
    /// 入った瞬間だけでなく、中に居る間に進行度が変わったり別イベントの再生が終わったりしたときも条件を見直す
    /// (同じ場所で続けて起きるイベントを、出入りし直さなくても発火させるため)。
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

        private Collider _playerInside; // トリガー内に居るプレイヤー(居なければ null)
        private bool _recheckPending; // 次フレームで条件を見直す
        private ProgressManager _progress;

        // 追加した時点で Is Trigger にする(付け忘れるとただの壁になり、エラーも出ずに発火しない)。
        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void OnValidate()
        {
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
                Debug.LogWarning(
                    $"[EventTrigger] '{name}': Collider の Is Trigger がオフです。オンにしないと発火しません。",
                    this
                );
        }

        private void OnEnable()
        {
            EventPlaybackService.PlayingChanged += OnPlayingChanged;
            _progress = ProgressManager.Instance;
            if (_progress != null)
                _progress.OnProgressChanged += OnProgressChanged;
        }

        private void OnDisable()
        {
            EventPlaybackService.PlayingChanged -= OnPlayingChanged;
            if (_progress != null)
                _progress.OnProgressChanged -= OnProgressChanged;
            _progress = null;
            _playerInside = null;
            _recheckPending = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(_playerTag))
                return;
            _playerInside = other;
            TryFire();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == _playerInside)
                _playerInside = null;
        }

        // 前のイベントは再生中に進行度を進め、その後で再生中フラグを戻す。進行度の変化時点ではまだ再生中で弾かれるので、
        // 再生終了のタイミングでも見直す。
        // 見直しは次フレームに回す: 通知を配っている最中に次のイベントを始めると、他の購読者には
        // 「開始(再生中)」の後に古い「終了(再生中でない)」が届いてしまうため、配り終わってから始める。
        private void OnPlayingChanged(bool playing)
        {
            if (!playing)
                _recheckPending = true;
        }

        private void OnProgressChanged() => _recheckPending = true;

        private void Update()
        {
            if (!_recheckPending)
                return;
            _recheckPending = false;
            if (_playerInside != null)
                TryFire();
        }

        private void TryFire()
        {
            if (_event == null)
                return;

            // 別イベントの再生中は発火しない(会話中にトリガーへ入り直したときの二重発火防止)。
            if (EventPlaybackService.IsPlaying)
                return;

            // Battle 中は新規イベントを発火しない(多重発火防止)。
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

            var player = EventPlayerService.Current;
            if (player == null)
            {
                Debug.LogWarning(
                    $"[EventTrigger] '{name}': IEventPlayer が見つからず発火をスキップ (event={_event.Id})。"
                        + " 常駐 EventPlayer(GameSession)が未生成です。"
                );
                return;
            }

            var spawn = _spawnPoint != null ? _spawnPoint : transform;
            player.Play(_event, new BattleSetup(_enemy, spawn.position, spawn.rotation));
        }
    }
}
