using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 剣の統括コンポーネント。接近・3段コンボは <see cref="MeleeWeaponController"/> の共通処理を使い、
    /// ここではガード・パリィの状態管理と、敵からの攻撃をガード/パリィで受け流す判定を担当する。
    /// </summary>
    public class SwordController : MeleeWeaponController
    {
        [Header("ガード・パリィ設定")]
        [Tooltip("ジャストパリィの受付時間（秒）")]
        public float parryWindowDuration = 0.2f;

        [Header("防御メーター")]
        [Tooltip("防御で受けるたびに減り、時間で回復する。0 になると防御が割れて怯む")]
        [SerializeField]
        private GuardGaugeSettings _guardGaugeSettings = new();

        [Header("エフェクト")]
        [Tooltip("ガード成功時に出すVFXプレハブ")]
        public GameObject guardEffectPrefab;

        [Tooltip("パリィ成功時に出すVFXプレハブ")]
        public GameObject parryEffectPrefab;

        [Header("音")]
        [Tooltip("ガードで攻撃を受けた時の音")]
        [SerializeField]
        private SoundCue _guardSound;

        [Tooltip("パリィ成功時の音")]
        [SerializeField]
        private SoundCue _parrySound;

        // ジャストパリィの残り受付時間(秒)。subAction を押した瞬間から数える
        private float _parryTimer;

        private GuardGauge _guardGauge;
        private PlayerFlinchHandler _flinchHandler;

        /// <summary>
        /// 防御メーター。剣が非表示のまま(Awake 前)でも HUD から読めるよう、初回アクセスで作る。
        /// </summary>
        public GuardGauge GuardGauge => _guardGauge ??= new GuardGauge(_guardGaugeSettings);

        protected override void Awake()
        {
            base.Awake();
            _flinchHandler = Player.GetComponent<PlayerFlinchHandler>();
            Assert.IsNotNull(_flinchHandler, $"{name}: PlayerFlinchHandler が見つかりません");
        }

        public override MeleeWeaponState CreateNeutralState() => new SwordStateFree(this);

        protected override void OnBeforeStateUpdate()
        {
            // パリィタイマーの更新（ステートに依存せず、ボタンの「押し始め」で受付開始）
            if (SubActionPressedThisFrame)
            {
                _parryTimer = parryWindowDuration;
            }
            else if (_parryTimer > 0f)
            {
                _parryTimer -= Time.deltaTime;
            }
        }

        /// <summary>
        /// 敵の攻撃判定（Hitbox等）から呼ばれ、ガード/パリィ判定を行う。
        /// ガードまたはパリィが成立した場合はtrueを返し、呼び出し元はダメージを与えない。
        /// falseの場合は無防備であり、呼び出し元がTakeDamageを行う。
        /// </summary>
        public bool ReceiveAttack(
            float damage,
            bool isMeleeAttack,
            Transform attacker,
            Vector3 hitPoint
        )
        {
            if (CurrentState is SwordStateGuard or SwordStateParry)
            {
                // ガード入力がされている、またはパリィ受付時間中のみ防御成立
                if (_parryTimer > 0f || Input.subAction)
                {
                    if (_parryTimer > 0f)
                    {
                        SpawnParryEffect(hitPoint);
                        WorldSoundPlayer.Play(_parrySound, hitPoint, this);

                        if (CameraShakeManager.Instance != null)
                            CameraShakeManager.Instance.Shake(0.4f);

                        if (isMeleeAttack && attacker != null)
                        {
                            // 崩れ方は敵ごとに異なるため、敵共通の受け口に任せる
                            var enemyCon = attacker.GetComponentInParent<EnemyBaseController>();
                            if (enemyCon != null)
                            {
                                enemyCon.ForceParried();
                            }
                        }

                        ChangeState(new SwordStateParry(this));

                        // 一回のガード入力で一回のパリィのみ受け付けるため、タイマーを0にする
                        _parryTimer = 0f;
                        return true;
                    }

                    SpawnGuardEffect(hitPoint);
                    WorldSoundPlayer.Play(_guardSound, hitPoint, this);

                    if (CameraShakeManager.Instance != null)
                        CameraShakeManager.Instance.Shake(0.2f);

                    // この一撃までは防ぐが、メーターが尽きたら防御が割れて怯む。
                    // 怯みの開始で武器は中立状態へ戻る(怯みの制限 → PlayerWeapons → ForceReset)
                    if (GuardGauge.Absorb(damage, Time.time))
                    {
                        _flinchHandler.TriggerFlinch();
                    }

                    return true;
                }
            }

            return false;
        }

        private void SpawnGuardEffect(Vector3 hitPosition)
        {
            if (guardEffectPrefab != null)
            {
                EffectPool.Play(guardEffectPrefab, hitPosition, PlayerTransform.rotation);
            }
        }

        private void SpawnParryEffect(Vector3 hitPosition)
        {
            if (parryEffectPrefab != null)
            {
                EffectPool.Play(parryEffectPrefab, hitPosition, PlayerTransform.rotation);
            }
        }

        /// <inheritdoc/>
        public override void ForceReset()
        {
            // 見た目は中立状態(SwordStateFree)の Enter で戻る
            base.ForceReset();
            _parryTimer = 0f;
        }
    }
}
