using CreativeAI.Core;
using CreativeAI.Core.EventSystem;
using CreativeAI.Core.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 扉に近づくと「扉を開ける」を出し、キーで開閉する(近接判定は <see cref="FieldItemPickup"/> と同じ Trigger + タグ判定)。
    /// 操作できるのは移動中(Field)だけで、戦闘中・会話中はプロンプトも出さない。
    /// 表示は <see cref="InteractPromptService"/> 越しに常駐UIへ渡す(ワールド側から UI アセンブリを参照できないため)。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class DoorInteractor : MonoBehaviour
    {
        [Tooltip("開閉する扉。未設定なら同じ GameObject から探す。")]
        [SerializeField]
        private SlidingDoor _door;

        [SerializeField]
        private string _playerTag = "Player";

        [Tooltip("閉じているときに出す文言。")]
        [SerializeField]
        private string _openLabel = "扉を開ける";

        [Tooltip("開いているときに出す文言。")]
        [SerializeField]
        private string _closeLabel = "扉を閉じる";

        [Tooltip("開閉に使うキー。")]
        [SerializeField]
        private Key _key = Key.E;

        private bool _playerInside;

        /// <summary>プレイヤーが範囲内にいる。</summary>
        public bool IsPlayerInside => _playerInside;

        private void Awake()
        {
            if (_door == null)
                _door = GetComponent<SlidingDoor>();
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
            _playerInside = false;
            InteractPromptService.Hide(this);
        }

        private void Update()
        {
            if (!CanInteract())
            {
                InteractPromptService.Hide(this);
                return;
            }

            InteractPromptService.Show(this, BuildLabel());

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[_key].wasPressedThisFrame)
                TryInteract();
        }

        /// <summary>いま操作を受け付けられるか(範囲内 / 移動中 / 会話も戦闘もしていない)。</summary>
        public bool CanInteract()
        {
            if (!_playerInside || _door == null)
                return false;
            if (
                GameModeManager.Instance != null
                && GameModeManager.Instance.CurrentMode != GameMode.Field
            )
                return false;
            return !EventPlaybackService.IsPlaying;
        }

        /// <summary>開閉を切り替える。受け付けられなければ何もせず false。</summary>
        public bool TryInteract()
        {
            if (!CanInteract())
                return false;
            _door.Toggle();
            InteractPromptService.Show(this, BuildLabel());
            return true;
        }

        private string BuildLabel()
        {
            string action = _door.IsOpen ? _closeLabel : _openLabel;
            return $"[{_key}] {action}";
        }
    }
}
