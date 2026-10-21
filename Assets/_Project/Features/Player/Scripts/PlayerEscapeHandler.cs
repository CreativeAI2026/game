using System.Collections;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 掴みから脱出したときの反応(脱出モーションと、それに合わせた後退)を担当する。
    /// 掴んでいた敵が <see cref="TriggerEscape"/> を呼ぶ。
    ///
    /// 脱出クリップ(Escaped_InPlace)は水平移動を切ってあるため、後退はここで CharacterController を動かして行う。
    /// 足の運びと移動がずれないよう、距離と時間は元クリップのルート移動に合わせてある。
    /// 後退中は全操作を制限する。重力は PlayerController が制限中もかけ続けるので、ここでは水平移動だけを扱う。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerEscapeHandler : MonoBehaviour
    {
        [Tooltip("脱出モーション中に後退する距離（m）。元クリップのルート移動量(約1.7m)に合わせている")]
        [SerializeField]
        private float _retreatDistance = 1.7f;

        [Tooltip(
            "後退にかける時間（秒）。元クリップでルートが動いている区間(先頭から約1.0秒。クリップ全長は約1.13秒)に合わせている"
        )]
        [SerializeField]
        private float _retreatDuration = 1.0f;

        private PlayerController _playerController;
        private CharacterController _characterController;
        private Animator _animator;
        private Coroutine _retreatCoroutine;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            // PlayerController は [RequireComponent(typeof(CharacterController))]
            _characterController = GetComponent<CharacterController>();
            _animator = PlayerModel.Of(this).Animator;
        }

        private void OnDisable() => Cancel();

        /// <summary>
        /// 脱出モーションを再生し、awayDirection(掴んでいた敵から離れる向き)へ後退させる。
        /// CharacterController を有効に戻してから呼ぶこと(掴み中に無効化している敵があるため)。
        /// </summary>
        public void TriggerEscape(Vector3 awayDirection)
        {
            awayDirection.y = 0f;
            awayDirection.Normalize();

            _animator.SetTrigger(PlayerAnimatorParams.Escaped);

            Cancel();
            _retreatCoroutine = StartCoroutine(RetreatCoroutine(awayDirection));
        }

        /// <summary>後退を途中で打ち切り、自分の行動制限を外す。</summary>
        public void Cancel()
        {
            if (_retreatCoroutine == null)
                return;

            StopCoroutine(_retreatCoroutine);
            _retreatCoroutine = null;
            _playerController.Locks.Release(this);
        }

        private IEnumerator RetreatCoroutine(Vector3 direction)
        {
            _playerController.Locks.Set(this, PlayerLock.All);

            // 元クリップのルートはほぼ等速で動いているので、等速で後退させる
            float speed = _retreatDistance / _retreatDuration;
            float elapsed = 0f;
            while (elapsed < _retreatDuration)
            {
                float step = Mathf.Min(Time.deltaTime, _retreatDuration - elapsed);
                _characterController.Move(direction * (speed * step));
                elapsed += step;
                yield return null;
            }

            _playerController.Locks.Release(this);
            _retreatCoroutine = null;
        }
    }
}
