using UnityEngine;

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

        [Header("ガード上限数設定")]
        [Tooltip("ガードの回数がこの数値以上になったら攻撃を受ける。")]
        public int guardMaxCount = 3;

        [Header("エフェクト")]
        [Tooltip("ガード成功時に出すVFXプレハブ")]
        public GameObject guardEffectPrefab;

        [Tooltip("パリィ成功時に出すVFXプレハブ")]
        public GameObject parryEffectPrefab;

        // ジャストパリィの残り受付時間(秒)。subAction を押した瞬間から数える
        private float _parryTimer;

        /// <summary>今回のガードで受けた回数。ガード開始時・待機に戻るときに 0 に戻す。</summary>
        public int GuardHitCount { get; set; }

        // 仮モデル専用の見た目補正。本番アニメーションで向きが付いたら外す(無ければ何もしない)
        private MockWeaponPose _mockPose;

        protected override void Awake()
        {
            base.Awake();
            TryGetComponent(out _mockPose);
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

        public override void OnAttackStateEntered()
        {
            SetMockPose(MockWeaponPoseKey.Attack);
        }

        public void SetMockPose(MockWeaponPoseKey key)
        {
            if (_mockPose != null)
                _mockPose.SetPose(key);
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

                        CameraShakeManager.Instance?.Shake(0.4f);

                        if (isMeleeAttack && attacker != null)
                        {
                            var enemyCon = attacker.GetComponentInParent<TestEnemyController>();
                            if (enemyCon != null)
                            {
                                enemyCon.ChangeState(new TestEnemyParriedState(enemyCon));
                            }
                        }

                        ChangeState(new SwordStateParry(this));

                        // 一回のガード入力で一回のパリィのみ受け付けるため、タイマーを0にする
                        _parryTimer = 0f;
                        return true;
                    }

                    SpawnGuardEffect(hitPoint);

                    CameraShakeManager.Instance?.Shake(0.2f);

                    GuardHitCount++;

                    // ガード耐久上限を超えたら強制的にガードを崩し、次の攻撃をダメージとして受ける
                    if (GuardHitCount >= guardMaxCount)
                    {
                        Debug.Log("ガード上限に達した");
                        // TODO : 専用アニメーションを追加する。
                        ChangeState(new SwordStateFree(this));
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
                // TODO : ObjectPool導入後、Instantiateをやめる
                Instantiate(guardEffectPrefab, hitPosition, PlayerTransform.rotation);
            }
        }

        private void SpawnParryEffect(Vector3 hitPosition)
        {
            if (parryEffectPrefab != null)
            {
                // TODO : ObjectPool導入後、Instantiateをやめる
                Instantiate(parryEffectPrefab, hitPosition, PlayerTransform.rotation);
            }
        }

        /// <inheritdoc/>
        public override void ForceReset()
        {
            // ガード回数・見た目は中立状態(SwordStateFree)の Enter で戻る
            base.ForceReset();
            _parryTimer = 0f;
        }
    }
}
