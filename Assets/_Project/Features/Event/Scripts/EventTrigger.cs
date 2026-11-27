using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// シーン上のトリガーに配置する非常駐コンポーネント。プレイヤー侵入を検知し、
    /// 条件(progress / flag / hasItem をすべて AND)を満たせばイベントを発火する。
    /// 中身の再生は持っている <see cref="EventPlayer"/> に任せ、自身のコルーチンとして回す。
    /// 無効になったら(シーンごと消えるときも)再生を打ち切る。
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
            "オンにすると、発火できる間だけマップピン(横に回って浮く2D画像)を出し、発火したら消す。"
        )]
        [SerializeField]
        private bool _showPin;

        [Tooltip(
            "ピンを出す位置(任意)。未設定ならこのトリガーの Collider の中心に出す。別の場所に出したいときに置く。"
        )]
        [SerializeField]
        private Transform _pinPoint;

        private MapPin _pin;
        private Collider _playerInside; // トリガー内に居るプレイヤー(居なければ null)
        private bool _recheckPending; // 次フレームで条件を見直す
        private ProgressManager _progress;
        private GameModeManager _gameMode;
        private readonly EventPlayer _player = new();

        // 追加した時点で Is Trigger にする(付け忘れるとただの壁になり、エラーも出ずに発火しない)。
        private void Reset()
        {
            if (TryGetComponent(out Collider col))
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
            if (_event != null && _event.Activation != EventActivation.Auto)
                Debug.LogWarning(
                    $"[EventTrigger] '{name}': イベント '{_event.Id}' は activation が interact です。"
                        + " 入っても発火しません。EventInteractor に付け替えてください。",
                    this
                );
        }

        private void OnEnable()
        {
            _gameMode = GameModeManager.Instance;
            if (_gameMode != null)
                _gameMode.Changed += OnModeChanged;
            _progress = ProgressManager.Instance;
            if (_progress != null)
                _progress.Changed += OnProgressChanged;
            _recheckPending = true; // 初回のピン表示も次フレームの見直しで決める
        }

        private void OnDisable()
        {
            // 再生中のまま消える(セーブ再開でシーンを読み直す等)と、続きを流さず進行度も進めずに打ち切る。
            StopAllCoroutines();
            _player.Abort();
            HidePin();
            if (_gameMode != null)
                _gameMode.Changed -= OnModeChanged;
            _gameMode = null;
            if (_progress != null)
                _progress.Changed -= OnProgressChanged;
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

        // 前のイベントは再生中に進行度を進め、その後でフィールドへ戻す。進行度の変化時点ではまだ会話中で弾かれるので、
        // フィールドへ戻ったタイミングでも見直す。
        // 見直しは次フレームに回す: 通知を配っている最中に次のイベントを始めると、他の購読者には
        // 「会話」の後に古い「フィールド」が届いてしまうため、配り終わってから始める。
        private void OnModeChanged()
        {
            if (GameModeManager.PlayerCanAct)
                _recheckPending = true;
        }

        private void OnProgressChanged() => _recheckPending = true;

        private void Update()
        {
            if (!_recheckPending)
                return;
            _recheckPending = false;
            RefreshPin();
            ForgetPlayerIfGone();
            if (_playerInside != null)
                TryFire();
        }

        /// <summary>
        /// 中に居るはずのプレイヤーが、実際には居なければ忘れる。
        /// player.moveTo(SpawnPoint.Place)は CharacterController を切って動かすので OnTriggerExit が届かず、
        /// 引き戻された後も「中に居る」と覚えたままになる。そのまま見直すと、終わるたびに同じイベントを撃ち直して引き戻しが止まらない。
        /// 判定は外接する箱(bounds)の重なりなので、箱以外(球・回転した形)では縁で「居る」側に寄る。トリガーは箱で置く前提。
        /// </summary>
        private void ForgetPlayerIfGone()
        {
            if (_playerInside == null)
                return;

            Physics.SyncTransforms(); // 直前に動かされた位置を Collider に反映してから判定する
            var gone =
                !_playerInside.enabled
                || !_playerInside.gameObject.activeInHierarchy
                || !GetComponent<Collider>().bounds.Intersects(_playerInside.bounds);
            if (gone)
                _playerInside = null;
        }

        /// <summary>ピンが出ているか。</summary>
        public bool PinVisible => _pin != null;

        /// <summary>
        /// ピンを出す/消す。「ピンあり」で、イベントの発火条件を満たしている間だけ出す。
        /// 進行度が進む(=発火済み)と条件を外れるので、再び出ることはない。
        /// </summary>
        public void RefreshPin()
        {
            var shouldShow = _showPin && _event != null && ConditionsMet();
            if (!shouldShow)
            {
                HidePin();
                return;
            }
            if (_pin != null)
                return;

            var position =
                _pinPoint != null ? _pinPoint.position : GetComponent<Collider>().bounds.center;
            _pin = MapPin.Create(position);
        }

        private void HidePin()
        {
            if (_pin != null)
                _pin.Remove();
            _pin = null;
        }

        private bool ConditionsMet() => EventAvailability.IsMet(_event);

        private void TryFire()
        {
            // キーで発火するイベントは EventInteractor の担当。入っただけでは発火しない。
            if (_event == null || _event.Activation != EventActivation.Auto)
                return;

            // 別イベントの再生中・戦闘中は発火しない(会話中にトリガーへ入り直したときなどの多重発火防止)。
            if (EventPlayer.Playing != null || !GameModeManager.PlayerCanAct)
                return;

            var progress = ProgressManager.Instance;
            if (progress == null)
            {
                Debug.LogWarning(
                    $"[EventTrigger] '{name}': ProgressManager.Instance が無いため条件評価不可 (event={_event.Id})."
                );
                return;
            }

            if (!ConditionsMet())
                return;

            HidePin();
            StartCoroutine(_player.Play(_event));
        }
    }
}
