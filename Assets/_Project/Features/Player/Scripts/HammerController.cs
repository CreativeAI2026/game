using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// ハンマーの統括コンポーネント。接近・3段コンボは <see cref="MeleeWeaponController"/> の共通処理を使い、
    /// ここでは subAction による叩きつけ(範囲攻撃)の演出・判定・跳躍先の計算を担当する。
    ///
    /// 叩きつけの流れ(タイミングはすべて Slam クリップの AnimationEvent):
    /// ThrustStart で噴射エフェクトを出す → LeapStart〜SlamImpact の区間で最寄りの敵の手前へ跳んで移動 →
    /// SlamImpact で地面の衝撃エフェクトと範囲攻撃判定を出す → TriggerDisableHitbox で判定を消す → ActionEnd。
    /// ステートの進行は <see cref="HammerStateSlam"/> が持つ。
    /// </summary>
    public class HammerController : MeleeWeaponController
    {
        [Header("叩きつけ: 跳躍")]
        [Tooltip("敵の中心からこの距離だけ手前に着地する（m）。敵に重なって着地しないための間合い")]
        [SerializeField]
        private float _leapStopDistance = 1.5f;

        [Tooltip("1回の跳躍で移動できる最大距離（m）")]
        [SerializeField]
        private float _maxLeapDistance = 8f;

        [Tooltip("跳躍の最高点の高さ（m）。見た目(モデル)だけを持ち上げ、接地・被弾判定はルートの位置(地面)に残す")]
        [SerializeField]
        private float _leapHeight = 1.8f;

        [Tooltip(
            "跳躍中の高さの変化。横軸: LeapStart→SlamImpact の進み(0〜1)、縦軸: 最高点に対する割合。"
                + "既定は素早く跳び上がり、頂点で少し溜めて、最後に急降下して叩きつける形"
        )]
        [SerializeField]
        private AnimationCurve _leapHeightCurve = new(
            new Keyframe(0f, 0f, 0f, 3.5f),
            new Keyframe(0.45f, 1f, 0f, 0f),
            new Keyframe(1f, 0f, -4.5f, 0f)
        );

        private PlayerModel _model;

        [Header("叩きつけ: エフェクト・判定")]
        [Tooltip("ハンマーの噴射エフェクト。ThrustStart で再生し、SlamImpact で放出を止める")]
        [SerializeField]
        private ParticleSystem _thrustEffect;

        [Tooltip(
            "叩きつけ時に地面へ出す衝撃エフェクトのプレハブ。範囲攻撃判定の真下(プレイヤーの足元の高さ)に出す"
        )]
        [SerializeField]
        private GameObject _impactEffectPrefab;

        [Tooltip(
            "範囲攻撃用の PlayerMeleeHitbox(3段攻撃用とは別)。半径を範囲攻撃の広さに合わせ、ハンマーヘッドの位置に置く"
        )]
        [SerializeField]
        private PlayerMeleeHitbox _slamHitbox;

        protected override void Awake()
        {
            base.Awake();
            Assert.IsNotNull(_thrustEffect, $"{name}: 噴射エフェクト(_thrustEffect)が未設定です");
            Assert.IsNotNull(
                _impactEffectPrefab,
                $"{name}: 衝撃エフェクト(_impactEffectPrefab)が未設定です"
            );
            Assert.IsNotNull(_slamHitbox, $"{name}: 範囲攻撃判定(_slamHitbox)が未設定です");
            _model = PlayerModel.Of(this);
        }

        /// <summary>跳躍の進み(0〜1)に応じて、見た目を跳躍の高さへ持ち上げる。</summary>
        public void ApplyLeapHeight(float progress)
        {
            _model.SetVisualHeightOffset(_leapHeightCurve.Evaluate(progress) * _leapHeight);
        }

        /// <summary>見た目の高さを地面に戻す(着地・中断時)。</summary>
        public void ResetLeapHeight()
        {
            _model.SetVisualHeightOffset(0f);
        }

        public override MeleeWeaponState CreateNeutralState() => new HammerStateFree(this);

        /// <summary>対象(いなければ何もしない)の方向へ、水平にプレイヤーを向ける。</summary>
        public void FaceTowards(Transform target)
        {
            if (target == null)
                return;

            Vector3 toTarget = target.position - PlayerTransform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
                PlayerTransform.rotation = Quaternion.LookRotation(toTarget);
        }

        /// <summary>
        /// 跳躍の着地点(水平位置)を決める。敵の手前 <see cref="_leapStopDistance"/> まで、最大 <see cref="_maxLeapDistance"/>。
        /// 敵がいない・既に間合いの内側なら、その場(現在位置)で叩きつける。
        /// </summary>
        public Vector3 ResolveLeapDestination(Transform target)
        {
            Vector3 start = PlayerTransform.position;
            if (target == null)
                return start;

            Vector3 toTarget = target.position - start;
            toTarget.y = 0f;
            float distance = Mathf.Min(toTarget.magnitude - _leapStopDistance, _maxLeapDistance);
            if (distance <= 0f)
                return start;

            return start + toTarget.normalized * distance;
        }

        public void StartThrustEffect()
        {
            _thrustEffect.Play();
        }

        /// <summary>噴射の放出だけを止める(出ている粒子は寿命まで残して自然に消す)。</summary>
        public void StopThrustEffect()
        {
            _thrustEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>地面を叩いた瞬間の処理。噴射を止め、衝撃エフェクトを出し、範囲攻撃判定を有効にする。</summary>
        public void ApplySlamImpact()
        {
            StopThrustEffect();

            CameraShakeManager.Instance?.Shake(1f);

            // 判定はハンマーヘッドに置くが、衝撃エフェクトは地面に出したいので高さだけ足元に合わせる
            Vector3 impactPoint = _slamHitbox.transform.position;
            impactPoint.y = PlayerTransform.position.y;
            // TODO : ObjectPool導入後、Instantiateをやめる
            Instantiate(_impactEffectPrefab, impactPoint, PlayerTransform.rotation);

            _slamHitbox.EnableHitbox();
        }

        public void DisableSlamHitbox()
        {
            _slamHitbox.DisableHitbox();
        }
    }
}
