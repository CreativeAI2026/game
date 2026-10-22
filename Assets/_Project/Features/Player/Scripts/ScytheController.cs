using SoundKit;
using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 鎌の統括コンポーネント。接近・3段コンボは <see cref="MeleeWeaponController"/> の共通処理を使い、
    /// ここでは subAction による投擲(ブーメラン)を担当する。
    ///
    /// 投擲の流れ: Throw ステート(投げモーション) → ThrowRelease イベントで手元の鎌を隠して
    /// <see cref="ScytheBoomerang"/> を飛ばす → ThrowHold イベントで投げモーションを止める
    /// → 戻ってきて受け取ったら手元の鎌を再表示し、続き(受け取りモーション)を ActionEnd まで再生。
    /// 投げてから受け取りモーションが終わるまで移動・攻撃・武器切替は不可。
    /// </summary>
    public class ScytheController : MeleeWeaponController
    {
        [Header("投擲")]
        [Tooltip(
            "投擲で狙う敵を探す半径（m）。近接のダッシュ接近(dashSearchRadius)とは別。"
                + "範囲内に敵がいなければ正面へ投げる。投擲体の最大飛距離に合わせるとよい"
        )]
        [SerializeField]
        private float _throwSearchRadius = 20f;

        [Tooltip("手に持っている鎌の見た目。投げている間は非表示にする")]
        [SerializeField]
        private GameObject _heldModel;

        [Tooltip("投げる鎌のプレハブ(ScytheBoomerang 付き)。開始時に1つだけ生成して使い回す")]
        [SerializeField]
        private ScytheBoomerang _boomerangPrefab;

        [Tooltip(
            "鎌を投げる音。ThrowRelease(手放した瞬間)で鳴らす。飛行中・受け止めの音は投擲用プレハブ側に割り当てる"
        )]
        [SerializeField]
        private SoundCue _throwSound;

        private ScytheBoomerang _boomerang;

        public bool IsBoomerangFlying => _boomerang.IsFlying;

        protected override void Awake()
        {
            base.Awake();
            Assert.IsNotNull(_heldModel, $"{name}: 手元の鎌モデル(_heldModel)が未設定です");
            Assert.IsNotNull(
                _boomerangPrefab,
                $"{name}: 投擲用プレハブ(_boomerangPrefab)が未設定です"
            );

            // プレイヤーの移動・回転に引きずられないよう、親なし(シーン直下)で生成する
            _boomerang = Instantiate(_boomerangPrefab);
            _boomerang.SetOwner(Player.GetComponent<PlayerStatus>());
            _boomerang.gameObject.SetActive(false);
            _boomerang.Caught += OnBoomerangCaught;
        }

        private void OnDestroy()
        {
            // シーン破棄時は投擲体(シーン直下の別オブジェクト)が先に破棄されている場合がある
            if (_boomerang != null)
            {
                _boomerang.Caught -= OnBoomerangCaught;
                Destroy(_boomerang.gameObject);
            }
        }

        protected override void OnDisable()
        {
            // 飛行中に武器が外れた(セーブ復元等)場合、鎌が戻らなくなるのを防ぐため即座に回収する。
            // シーン破棄時は投擲体が先に破棄されている場合があるため null を確認する
            if (_boomerang != null)
                _boomerang.ForceCatch();

            Animator.ResetTrigger(PlayerAnimatorParams.Throw);
            // 止めたまま外れると、次に投げたときの Throw ステートが動かなくなる
            SetThrowAnimationPaused(false);

            base.OnDisable();
        }

        /// <summary>鎌が飛んでいる間は「帰還待ち」が中立状態(怯みが明けても移動・再投擲させない)。</summary>
        public override MeleeWeaponState CreateNeutralState() =>
            _boomerang.IsFlying ? new ScytheStateWaitReturn(this) : new ScytheStateFree(this);

        /// <summary>投擲で狙う敵を <see cref="_throwSearchRadius"/> 内の最寄りの敵から探す。</summary>
        public Transform FindThrowTarget() => FindNearestEnemy(_throwSearchRadius);

        /// <summary>
        /// <paramref name="from"/> から見た投擲方向を決める。敵がいればその方向、いなければキャラクターの正面。
        /// 高さの差は無視して水平に飛ばす(地面や頭上へ飛んでいかないように)。
        /// </summary>
        public Vector3 ResolveThrowDirection(Transform target, Vector3 from)
        {
            if (target != null && target.gameObject.activeInHierarchy)
            {
                Vector3 toTarget = target.position - from;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                    return toTarget.normalized;
            }
            return PlayerTransform.forward;
        }

        /// <summary>
        /// 手元の鎌を隠し、この武器ルートの位置から <paramref name="target"/> へ投擲体を飛ばす。戻り先もこの武器ルート(手元)。
        /// 方向はプレイヤーの中心ではなく投げ始めの位置から決める(中心基準だと、手元の横ずれの分だけ敵の横を素通りする)。
        /// </summary>
        public void LaunchBoomerang(Transform target)
        {
            Vector3 origin = transform.position;
            _heldModel.SetActive(false);
            _boomerang.Launch(origin, ResolveThrowDirection(target, origin), transform);
            WorldSoundPlayer.Play(_throwSound, origin, this);
        }

        /// <summary>
        /// 投げモーション(Throw ステート)を止める/続きを再生する。
        /// Animator.speed は全レイヤー・全ステートが止まる(怯みのモーションも動かなくなる)ため、
        /// Throw ステートだけに効く速度倍率パラメータで止める。
        /// </summary>
        public void SetThrowAnimationPaused(bool paused) =>
            Animator.SetFloat(PlayerAnimatorParams.ThrowSpeed, paused ? 0f : 1f);

        /// <summary>
        /// Animator が投げモーションを再生中か。遷移中は遷移先で判定する
        /// (Throw へ入る途中は true、Throw から移動モーションへ戻る途中は false)。
        /// </summary>
        public bool IsThrowAnimationPlaying()
        {
            AnimatorStateInfo state = Animator.IsInTransition(0)
                ? Animator.GetNextAnimatorStateInfo(0)
                : Animator.GetCurrentAnimatorStateInfo(0);
            return state.shortNameHash == PlayerAnimatorStates.Throw;
        }

        private void OnBoomerangCaught()
        {
            _heldModel.SetActive(true);
        }
    }
}
