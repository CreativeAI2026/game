using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスのわっかに締め付けられている間のプレイヤーの拘束。
    /// 操作を止め、締め付けで継続ダメージを与え、レバガチャ(<see cref="MashEscapeGauge"/>)で抜け出させる。
    ///
    /// TutorialBoss の掴みと違い、カメラの演出は使わず、抜け出しても後ろへ下がらない(高台の端から落ちてしまうため)。
    /// わっかの見た目の動きは <see cref="KurageHaloRing"/> の担当。
    /// </summary>
    [RequireComponent(typeof(EnemyStatus))]
    public class KurageRingRestraint : MonoBehaviour
    {
        [Header("レバガチャ")]
        [Tooltip("抜け出すのに必要なゲージの量")]
        [SerializeField]
        private float _escapeThreshold = 20f;

        [Tooltip("入力の向きを1回変えるごとに溜まる量")]
        [SerializeField]
        private float _escapePerInput = 1f;

        [Header("締め付けダメージ")]
        [Tooltip("ダメージを与える間隔（秒）")]
        [SerializeField]
        private float _damageInterval = 0.5f;

        [Tooltip("1回のダメージ。攻撃力に掛ける倍率")]
        [SerializeField]
        private float _damageMultiplier = 0.15f;

        private readonly MashEscapeGauge _gauge = new MashEscapeGauge();
        private EnemyStatus _status;
        private PlayerController _playerController;
        private PlayerInputHandler _playerInput;
        private PlayerStatus _playerStatus;
        private PlayerEscapeHandler _playerEscape;
        private float _damageTimer;

        /// <summary>レバガチャで抜け出した。</summary>
        public event Action Escaped;

        /// <summary>抜け出す以外の理由(プレイヤーが倒れた・ボスが倒れた)で拘束が終わった。</summary>
        public event Action Released;

        public bool IsRestraining { get; private set; }

        private void Awake()
        {
            _status = GetComponent<EnemyStatus>();
            Assert.IsNotNull(_status, $"{name}: EnemyStatus が同じ GameObject にありません");
        }

        /// <summary>わっかがプレイヤーを締めた時に呼ぶ。</summary>
        public void Begin(Transform player)
        {
            if (IsRestraining)
                return;

            _playerController = player.GetComponent<PlayerController>();
            _playerInput = player.GetComponent<PlayerInputHandler>();
            _playerStatus = player.GetComponent<PlayerStatus>();
            _playerEscape = player.GetComponent<PlayerEscapeHandler>();
            Assert.IsNotNull(_playerController, $"{player.name}: PlayerController がありません");
            Assert.IsNotNull(_playerInput, $"{player.name}: PlayerInputHandler がありません");
            Assert.IsNotNull(_playerStatus, $"{player.name}: PlayerStatus がありません");
            Assert.IsNotNull(_playerEscape, $"{player.name}: PlayerEscapeHandler がありません");

            // 掴まれている扱いにして、武器の攻撃や焦り度の判定を掴みと同じに止める
            _playerController.IsGrabbed = true;
            _playerController.Locks.Set(this, PlayerLock.All);

            PlayerModel playerModel = player.GetComponentInChildren<PlayerModel>();
            if (playerModel != null)
                playerModel.Animator.SetTrigger(PlayerAnimatorParams.Grabbed);

            _gauge.Reset(_escapeThreshold, _escapePerInput);
            _damageTimer = 0f;
            IsRestraining = true;
            GrabEscapeEvents.OnShowGauge?.Invoke(0f, _gauge.Threshold);
        }

        /// <summary>プレイヤーを放す(ボスが倒れた時など)。</summary>
        public void Cancel()
        {
            if (!IsRestraining)
                return;
            End();
            Released?.Invoke();
        }

        private void Update()
        {
            if (!IsRestraining)
                return;

            if (_playerStatus.IsDead)
            {
                Cancel();
                return;
            }

            _damageTimer += Time.deltaTime;
            if (_damageTimer >= _damageInterval)
            {
                _damageTimer -= _damageInterval;
                // 拘束中に怯みモーションへ移ると掴まれモーションが崩れるため、怯ませない
                _playerStatus.TakeDamage(
                    _status.CurrentAttackPower * _damageMultiplier,
                    false,
                    suppressFlinch: true
                );
                if (_playerStatus.IsDead)
                {
                    Cancel();
                    return;
                }
            }

            if (_gauge.Feed(_playerInput.move))
            {
                GrabEscapeEvents.OnUpdateGauge?.Invoke(_gauge.Value, _gauge.Threshold);
                if (_gauge.IsFull)
                {
                    End();
                    _playerEscape.TriggerEscapeInPlace();
                    Escaped?.Invoke();
                }
            }
        }

        private void End()
        {
            IsRestraining = false;
            GrabEscapeEvents.OnHideGauge?.Invoke();
            _playerController.IsGrabbed = false;
            _playerController.Locks.Release(this);
        }

        private void OnDisable() => Cancel();
    }
}
