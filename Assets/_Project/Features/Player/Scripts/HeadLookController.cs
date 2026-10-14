using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// Animation Rigging のLook At ターゲットを動的に制御し、
    /// 近くの敵がいれば視線を向け、いなければ正面を見るようにする。
    /// 可動域を制限することで、首が不自然に回りすぎるのを防ぐ。
    ///
    /// 頭のボーンと注視ターゲットはモデル層(<see cref="PlayerModel"/>)から取るので、モデルを差し替えても設定し直さなくてよい。
    /// </summary>
    [RequireComponent(typeof(PlayerTargetManager))]
    public class HeadLookController : MonoBehaviour
    {
        // 可動域外の敵を見るとき、方向だけを表す仮想ポイントを置く距離。Look At は方向のみ参照するため値自体は重要ではない
        private const float VirtualTargetDistance = 5f;

        [Header("可動域設定")]
        [Tooltip("正面から首を向ける最大角度（左右・上下）")]
        public float maxAngle = 60f;

        [Tooltip("視線移動の滑らかさ")]
        public float smoothSpeed = 5f;

        private Transform _headBone;
        private Transform _lookTarget;
        private PlayerTargetManager _targetManager;

        // ボーンの取得は Animator の初期化後(Start)に行う
        private void Start()
        {
            PlayerModel model = PlayerModel.Of(this);
            _headBone = model.GetBone(HumanBodyBones.Head);
            _lookTarget = model.LookAtTarget;
            _targetManager = GetComponent<PlayerTargetManager>();
        }

        private void LateUpdate()
        {
            Vector3 targetPosition;

            // 敵の足元ではなく体の中心を見る
            Vector3? enemyPoint = _targetManager.CurrentTargetPoint;
            if (enemyPoint.HasValue)
            {
                Vector3 dirToEnemy = enemyPoint.Value - _headBone.position;

                float angleToEnemy = Vector3.Angle(transform.forward, dirToEnemy);

                if (angleToEnemy <= maxAngle)
                {
                    targetPosition = enemyPoint.Value;
                }
                else
                {
                    // 可動域を超えた敵に対しては、限界角度の境界にターゲットを留める。
                    // こうしないと首が180度回転する等の不自然な見た目になる
                    Vector3 clampedDir = Vector3.RotateTowards(
                        transform.forward,
                        dirToEnemy,
                        maxAngle * Mathf.Deg2Rad,
                        0f
                    );

                    targetPosition = _headBone.position + clampedDir * VirtualTargetDistance;
                }
            }
            else
            {
                targetPosition = _headBone.position + transform.forward * VirtualTargetDistance;
            }

            // Lerpで補間することで、ターゲットが切り替わった瞬間に首がカクつくのを防ぐ
            _lookTarget.position = Vector3.Lerp(
                _lookTarget.position,
                targetPosition,
                Time.deltaTime * smoothSpeed
            );
        }
    }
}
