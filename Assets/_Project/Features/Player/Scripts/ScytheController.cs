using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 鎌の統括コンポーネント。接近・3段コンボは <see cref="MeleeWeaponController"/> の共通処理を使い、
    /// ここでは subAction による投擲(ブーメラン)を担当する。
    ///
    /// 投擲の流れ: Throw ステート(投げモーション) → ThrowRelease イベントで手元の鎌を隠して
    /// <see cref="ScytheBoomerang"/> を飛ばす → 戻ってきて受け取ったら手元の鎌を再表示。
    /// 投げてから受け取るまで移動・攻撃・武器切替は不可。
    /// </summary>
    public class ScytheController : MeleeWeaponController
    {
        [Header("投擲")]
        [Tooltip("手に持っている鎌の見た目。投げている間は非表示にする")]
        [SerializeField]
        private GameObject _heldModel;

        [Tooltip("投げる鎌のプレハブ(ScytheBoomerang 付き)。開始時に1つだけ生成して使い回す")]
        [SerializeField]
        private ScytheBoomerang _boomerangPrefab;

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

            base.OnDisable();
        }

        /// <summary>鎌が飛んでいる間は「帰還待ち」が中立状態(怯みが明けても移動・再投擲させない)。</summary>
        public override MeleeWeaponState CreateNeutralState() =>
            _boomerang.IsFlying ? new ScytheStateWaitReturn(this) : new ScytheStateFree(this);

        /// <summary>
        /// 投擲方向を決める。索敵範囲内に敵がいればその方向、いなければキャラクターの正面。
        /// 高さの差は無視して水平に飛ばす(地面や頭上へ飛んでいかないように)。
        /// </summary>
        public Vector3 ResolveThrowDirection(Transform target)
        {
            if (target != null && target.gameObject.activeInHierarchy)
            {
                Vector3 toTarget = target.position - PlayerTransform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                    return toTarget.normalized;
            }
            return PlayerTransform.forward;
        }

        /// <summary>手元の鎌を隠し、この武器ルートの位置から投擲体を飛ばす。戻り先もこの武器ルート(手元)。</summary>
        public void LaunchBoomerang(Vector3 direction)
        {
            _heldModel.SetActive(false);
            _boomerang.Launch(transform.position, direction, transform);
        }

        private void OnBoomerangCaught()
        {
            _heldModel.SetActive(true);
        }
    }
}
