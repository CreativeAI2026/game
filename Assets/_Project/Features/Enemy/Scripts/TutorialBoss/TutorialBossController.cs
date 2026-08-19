using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// チュートリアルボス専用のAIコントローラ。
    /// 子オブジェクトの懐中電灯（Spotlight）による視界判定と、
    /// SoundEventBusを通じた音への反応を主軸とした行動制御を行う。
    /// </summary>
    public class TutorialBossController : EnemyBaseController
    {
        [Header("懐中電灯（視界）設定")]
        [Tooltip("懐中電灯オブジェクトのTransform。回転制御に使用する。")]
        [SerializeField]
        private Transform flashlightTransform;

        [Tooltip("懐中電灯のLightコンポーネント。SpotAngleとRangeを視界範囲として使用する。")]
        [SerializeField]
        private Light flashlightLight;

        [Tooltip("プレイヤーが光から外れてから見失うまでの時間（秒）。")]
        [SerializeField]
        private float lostSightDuration = 3f;

        [Header("移動設定")]
        [Tooltip("未発見時の歩行速度。")]
        [SerializeField]
        private float walkSpeed = 1.5f;

        [Tooltip("発見時・音反応時の走り速度。")]
        [SerializeField]
        private float runSpeed = 5f;

        [Tooltip("発見時様子見移動の速度")]
        [SerializeField]
        private float strafeSpeed = 2f;

        [Header("距離設定")]
        [Tooltip("通常攻撃の攻撃射程。")]
        [SerializeField]
        private float attackRange = 2f;

        [Tooltip("通常攻撃2（腕伸ばし）の判定射程。")]
        [SerializeField]
        private float normalAttack2Range = 4f;

        [Tooltip("特殊攻撃（触手）の最大射程。")]
        [SerializeField]
        private float specialAttackRange = 10f;

        [Header("タイマー設定")]
        [Tooltip("様子見ステートの継続時間（秒）。")]
        [SerializeField]
        private float watchDuration = 2f;

        [Header("攻撃確率設定")]
        [Tooltip("攻撃ステート選択時に特殊攻撃を選ぶ確率（0〜1）。")]
        [SerializeField]
        [Range(0f, 1f)]
        private float specialAttackChance = 0.3f;

        [Tooltip("攻撃ステート選択時に通常攻撃2を選ぶ確率（0〜1）。特殊攻撃が外れた後に判定する。")]
        [SerializeField]
        [Range(0f, 1f)]
        private float normalAttack2Chance = 0.4f;

        [Header("触手（特殊攻撃）設定")]
        [Tooltip("触手を描画するLineRendererのリスト（5本分アサインする）。")]
        [SerializeField]
        private List<LineRenderer> tentacleLineRenderers = new List<LineRenderer>();

        [Tooltip("触手の根元となるTransform。攻撃用の腕の逆の腕先端に設定する。")]
        [SerializeField]
        private Transform tentacleOrigin;

        [Tooltip("特殊攻撃の狙いフェーズの時間（秒）。")]
        [SerializeField]
        private float specialAttackAimDuration = 1.5f;

        [Tooltip("特殊攻撃の射出フェーズの時間（秒）。")]
        [SerializeField]
        private float specialAttackShootDuration = 0.3f;

        [Header("音反応設定")]
        [Tooltip("反応する音の最大半径フィルタ（未実装の半径フィルタ用）。")]
        [SerializeField]
        private float soundReactRadius = 15f;

        [Header("障害物設定")]
        [SerializeField]
        private LayerMask obstacleLayer;

        [Header("掴み（捕獲）設定")]
        [Tooltip("電撃ダメージ（1ティックあたり）。")]
        [SerializeField]
        private float grabDamagePerTick = 5f;

        [Tooltip("電撃ダメージを与える間隔（秒）。")]
        [SerializeField]
        private float grabDamageInterval = 0.5f;

        [Tooltip("脱出に必要なゲージの最大値。")]
        [SerializeField]
        private float grabEscapeThreshold = 20f;

        [Tooltip("移動ボタン1入力あたりのゲージ増加量。")]
        [SerializeField]
        private float grabEscapePerInput = 1f;

        [Tooltip("プレイヤーを引き寄せる際にかかる時間（秒）。")]
        [SerializeField]
        private float grabPullDuration = 0.5f;

        [Tooltip("プレイヤーを引き寄せる目標距離（ボス正面方向）。")]
        [SerializeField]
        private float grabPullDistance = 1.5f;

        [Tooltip(
            "プレイヤーを引き寄せる目標の横方向オフセット（ボスのright方向）。正値で右、負値で左にずれる。カメラ演出に応じて調整する。"
        )]
        [SerializeField]
        private float grabPullLateralOffset = 0f;

        [Tooltip("脱出後にプレイヤーを後方へ押し出す力。")]
        [SerializeField]
        private float grabEscapeKnockbackForce = 8f;

        [Tooltip("電撃エフェクトの ParticleSystem プレハブ（プレイヤーにアタッチして使用）。")]
        [SerializeField]
        private ParticleSystem electricEffectPrefab;

        [Tooltip("触手の引っ込み演出にかかる時間（秒）。捕獲脱出後のリトラクト。")]
        [SerializeField]
        private float grabRetractDuration = 0.4f;

        // 外部参照用
        public Transform FlashlightTransform => flashlightTransform;
        public Light FlashlightLight => flashlightLight;
        public float LostSightDuration => lostSightDuration;
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float StrafeSpeed => strafeSpeed;
        public float AttackRange => attackRange;
        public float NormalAttack2Range => normalAttack2Range;
        public float SpecialAttackRange => specialAttackRange;
        public float WatchDuration => watchDuration;
        public float SpecialAttackChance => specialAttackChance;
        public float NormalAttack2Chance => normalAttack2Chance;
        public List<LineRenderer> TentacleLineRenderers => tentacleLineRenderers;
        public Transform TentacleOrigin => tentacleOrigin;
        public float SpecialAttackAimDuration => specialAttackAimDuration;
        public float SpecialAttackShootDuration => specialAttackShootDuration;
        public float SoundReactRadius => soundReactRadius;
        public LayerMask ObstacleLayer => obstacleLayer;
        public float GrabDamagePerTick => grabDamagePerTick;
        public float GrabDamageInterval => grabDamageInterval;
        public float GrabEscapeThreshold => grabEscapeThreshold;
        public float GrabEscapePerInput => grabEscapePerInput;
        public float GrabPullDuration => grabPullDuration;
        public float GrabPullDistance => grabPullDistance;
        public float GrabPullLateralOffset => grabPullLateralOffset;
        public float GrabEscapeKnockbackForce => grabEscapeKnockbackForce;
        public ParticleSystem ElectricEffectPrefab => electricEffectPrefab;
        public float GrabRetractDuration => grabRetractDuration;

        //  内部状態（ステートから読み書き）
        /// <summary>最後に聴取した音源のワールド座標。SoundInvestigateStateの目標地点として使う。</summary>
        [HideInInspector]
        public Vector3 LastHeardSoundPosition;

        /// <summary>プレイヤーが光の外にいる継続時間のカウンタ。</summary>
        [HideInInspector]
        public float LostSightTimer;

        // ────────────────────────────────────────────
        //  Unity ライフサイクル
        // ────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Start()
        {
            base.Start();
            ChangeState(new TBossPatrolState(this));
        }

        protected override void OnEnable()
        {
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        protected override void Update()
        {
            base.Update();
        }

        // ────────────────────────────────────────────
        //  EnemyBaseController オーバーライド
        // ────────────────────────────────────────────

        public override void ForceFlinch()
        {
            base.ForceFlinch();
            ChangeState(new TBossFlinchState(this));
        }

        public override void ForceAlert()
        {
            base.ForceAlert();
            if (!IsAlerted)
            {
                IsAlerted = true;
                ChangeState(new TBossChaseState(this));
            }
        }

        public override void ForceDeath()
        {
            base.ForceDeath();
            ChangeState(new TBossDeathState(this));
        }

        // ────────────────────────────────────────────
        //  視界判定
        // ────────────────────────────────────────────

        /// <summary>
        /// プレイヤーが懐中電灯の光円錐（SpotAngle・Range）内にいるかを判定する。
        /// 障害物による遮蔽も考慮する。
        /// </summary>
        public bool CheckInFlashlight()
        {
            if (Player == null || flashlightLight == null || flashlightTransform == null)
            {
                return false;
            }

            Vector3 toPlayer = Player.transform.position - flashlightTransform.position;
            float distance = toPlayer.magnitude;

            // 射程外
            if (distance > flashlightLight.range)
            {
                return false;
            }

            // 角度判定（SpotAngle は全体の角度なので半角で比較）
            float angle = Vector3.Angle(flashlightTransform.forward, toPlayer.normalized);
            if (angle > flashlightLight.spotAngle * 0.5f)
            {
                return false;
            }

            // 障害物による遮蔽チェック
            Vector3 rayStart = flashlightTransform.position;
            Vector3 rayTarget = Player.transform.position + Vector3.up * 1f;
            Vector3 rayDir = (rayTarget - rayStart).normalized;
            if (Physics.Raycast(rayStart, rayDir, out RaycastHit hit, distance, obstacleLayer))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 懐中電灯を指定のワールド座標方向へ向ける。
        /// 水平方向のみ回転する（縦方向には動かさない）。
        /// 毎フレーム呼び出すことで滑らかな追従を実現する。
        /// </summary>
        /// <param name="targetWorldPos">向けたいワールド座標</param>
        /// <param name="rotateSpeed">補間速度（デフォルト5）</param>
        public void RotateFlashlightToward(Vector3 targetWorldPos, float rotateSpeed = 5f)
        {
            // スクリプトで強制的にライトの向きを変えるとアニメーションと競合してバグるため、
            // 処理を削除し、ライトは常に体の向きに固定されるようにする。
        }

        /// <summary>
        /// 攻撃ステートをランダムに選択して遷移する。
        /// （SpecialAttack → NormalAttack2 → NormalAttack の順で確率判定）
        /// </summary>
        public void TransitionToAttack()
        {
            if (Random.value <= specialAttackChance)
            {
                ChangeState(new TBossSpecialAttackState(this));
            }
            else if (Random.value <= normalAttack2Chance)
            {
                ChangeState(new TBossNormalAttack2State(this));
            }
            else
            {
                ChangeState(new TBossNormalAttackState(this));
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 攻撃射程
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // 通常攻撃2射程
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, normalAttack2Range);

            // 特殊攻撃射程
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, specialAttackRange);

            // 音反応半径
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, soundReactRadius);
        }
#endif
    }
}
