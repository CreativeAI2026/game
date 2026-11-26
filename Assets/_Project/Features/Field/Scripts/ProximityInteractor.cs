using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 近づくと操作プロンプトを出し、キーで何かを実行する対象の共通部分(近接判定は <see cref="FieldItemPickup"/> と同じ Trigger + タグ判定)。
    /// 操作できるのは移動中(Field)だけで、戦闘中・会話中はプロンプトも出さない。
    /// 表示は <see cref="InteractPromptService"/> 越しに常駐UIへ渡す(ワールド側から UI アセンブリを参照できないため)。
    /// 何を出して何を実行するかは派生クラスが決める。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class ProximityInteractor : MonoBehaviour
    {
        [SerializeField]
        private string _playerTag = "Player";

        private PlayerInputHandler _inputHandler;
        private PlayerInput _playerInput;
        private bool _playerInside;

        private InputAction _interactAction;

        // 毎フレーム文字列を作らないよう、入力の種類と文言が同じ間は前回のプロンプトを使う
        private string _cachedScheme;
        private string _cachedActionLabel;
        private string _cachedLabel;

        /// <summary>操作の対象が揃っているか(扉なら動かす扉がある、など)。揃っていなければ何も出さない。</summary>
        protected abstract bool HasTarget { get; }

        /// <summary>いま出すプロンプトの動作部分(「扉を開ける」など。キー表示は基底が付ける)。</summary>
        protected abstract string ActionLabel { get; }

        /// <summary>操作を実行する。</summary>
        protected abstract void Perform();

        private void OnEnable()
        {
            PlayerRegistry.Changed += BindPlayer;
            BindPlayer(PlayerRegistry.Current);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || !other.CompareTag(_playerTag))
                return;
            _playerInside = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null || !other.CompareTag(_playerTag))
                return;
            _playerInside = false;
            InteractPromptService.Hide(this);
        }

        private void OnDisable()
        {
            PlayerRegistry.Changed -= BindPlayer;
            BindPlayer(null);
            _playerInside = false;
            InteractPromptService.Hide(this);
        }

        private void BindPlayer(PlayerController player)
        {
            if (_inputHandler != null)
                _inputHandler.InteractPressed -= OnInteractPressed;

            _inputHandler = player != null ? player.GetComponent<PlayerInputHandler>() : null;
            _playerInput = player != null ? player.GetComponent<PlayerInput>() : null;
            _interactAction =
                _playerInput != null ? _playerInput.actions.FindAction("Interact") : null;
            _cachedLabel = null;

            if (_inputHandler != null)
                _inputHandler.InteractPressed += OnInteractPressed;
        }

        private void OnInteractPressed()
        {
            if (!UIPanelService.IsOpen)
                TryInteract();
        }

        private void Update()
        {
            if (!CanInteract())
            {
                InteractPromptService.Hide(this);
                return;
            }

            InteractPromptService.Show(this, BuildLabel());
        }

        /// <summary>いま操作を受け付けられるか(範囲内 / 移動中 / 会話も戦闘もしていない)。</summary>
        public bool CanInteract()
        {
            if (!_playerInside || !HasTarget)
                return false;
            return EventPlaybackService.IsFreeRoaming;
        }

        /// <summary>操作を実行する。受け付けられなければ何もせず false。</summary>
        public bool TryInteract()
        {
            if (!CanInteract())
                return false;
            Perform();
            InteractPromptService.Show(this, BuildLabel());
            return true;
        }

        // キー表示は入力設定(Interact)から取る。キーを変えても表示がずれないように、ここには書かない。
        private string BuildLabel()
        {
            string scheme = _playerInput != null ? _playerInput.currentControlScheme : null;
            string actionLabel = ActionLabel;
            if (
                _cachedLabel != null
                && _cachedScheme == scheme
                && _cachedActionLabel == actionLabel
            )
                return _cachedLabel;

            _cachedScheme = scheme;
            _cachedActionLabel = actionLabel;
            _cachedLabel =
                _interactAction != null ? $"[{KeyName(scheme)}] {actionLabel}" : actionLabel;
            return _cachedLabel;
        }

        private string KeyName(string scheme)
        {
            // Xbox / PS4 の操作方式もゲームパッドの割り当て(Gamepad)を使っている
            string group =
                string.IsNullOrEmpty(scheme) || scheme == "KeyboardMouse"
                    ? "KeyboardMouse"
                    : "Gamepad";
            return _interactAction.GetBindingDisplayString(InputBinding.MaskByGroup(group));
        }
    }
}
