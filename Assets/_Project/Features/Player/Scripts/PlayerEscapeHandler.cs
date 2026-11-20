using System.Collections;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 掴みから脱出したときの反応(脱出モーションと、それに合わせた後退)を担当する。
    /// 掴んでいた敵が <see cref="TriggerEscape"/> を呼ぶ。
    /// 敵にはじき返された時も同じモーションと後退を使う(<see cref="TriggerRepelled"/>)。
    ///
    /// 脱出クリップ(Escaped_InPlace)は水平移動を切ってあるため、後退はここで CharacterController を動かして行う。
    /// 足の運びと移動がずれないよう、距離と時間は元クリップのルート移動に合わせてある。
    /// 後退中は全操作を制限する。重力は PlayerController が制限中もかけ続けるので、ここでは水平移動だけを扱う。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(WeaponManager))]
    [RequireComponent(typeof(PlayerFlinchHandler))]
    public class PlayerEscapeHandler : MonoBehaviour
    {
        [Tooltip(
            "脱出モーション中に後退する距離（m）。元クリップのルート移動量(約1.7m)に合わせている"
        )]
        [SerializeField]
        private float _retreatDistance = 1.7f;

        [Tooltip(
            "後退にかける時間（秒）。元クリップでルートが動いている区間(先頭から約1.0秒。クリップ全長は約1.13秒)に合わせている"
        )]
        [SerializeField]
        private float _retreatDuration = 1.0f;

        private PlayerController _playerController;
        private CharacterController _characterController;
        private WeaponManager _weaponManager;
        private PlayerFlinchHandler _flinchHandler;
        private Animator _animator;
        private Coroutine _retreatCoroutine;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            // PlayerController は [RequireComponent(typeof(CharacterController))]
            _characterController = GetComponent<CharacterController>();
            _weaponManager = GetComponent<WeaponManager>();
            _flinchHandler = GetComponent<PlayerFlinchHandler>();
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
            StartRetreat(awayDirection);
        }

        /// <summary>
        /// 敵にはじき返された。掴みから脱出した時と同じモーションを、どの状態からでも再生し、
        /// 敵の方を向いたまま awayDirection(敵から離れる向き)へ後退させる。その間は全操作を制限する。
        /// 敵がパリィされて崩れるのと同じく、プレイヤー側の隙として見せる。
        /// </summary>
        public void TriggerRepelled(Vector3 awayDirection)
        {
            awayDirection.y = 0f;
            awayDirection.Normalize();

            // 後ろへ下がるモーションなので、敵の方へ向き直してから再生する
            if (awayDirection != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(-awayDirection);

            // 怯みが続いていると、怯みの終わりに「移動へ戻る」を送って脱出モーションを打ち切ってしまう
            _flinchHandler.Cancel();

            // コンボ中・ガード中・パリィ中・エイム中でも抜けられるよう、怯みと同じく武器を中立状態に戻す
            _weaponManager.ResetEquippedWeapon();

            // 直前の行動の「移動へ戻る」指示が残っていると、脱出モーションに入った直後に抜けてしまう
            _animator.ResetTrigger(PlayerAnimatorParams.ReturnToLocomotion);
            _animator.SetTrigger(PlayerAnimatorParams.Repelled);
            StartRetreat(awayDirection);
        }

        /// <summary>
        /// 脱出モーションだけを再生し、その場から動かさない。高台の端など、後ろへ下がると落ちてしまう拘束で使う。
        /// モーション中の操作制限は後退する時と同じ時間かける。
        /// </summary>
        public void TriggerEscapeInPlace() => TriggerEscape(Vector3.zero);

        /// <summary>後退を途中で打ち切り、自分の行動制限を外す。</summary>
        public void Cancel()
        {
            if (_retreatCoroutine == null)
                return;

            StopCoroutine(_retreatCoroutine);
            _retreatCoroutine = null;
            _playerController.Locks.Release(this);
        }

        private void StartRetreat(Vector3 direction)
        {
            Cancel();
            _retreatCoroutine = StartCoroutine(RetreatCoroutine(direction));
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
