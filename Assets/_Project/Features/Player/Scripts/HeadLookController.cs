using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// Animation Rigging のLook At ターゲットを動的に制御し、
    /// 近くの敵がいれば視線を向け、いなければ正面を見るようにする。
    /// 可動域を制限することで、首が不自然に回りすぎるのを防ぐ。
    /// </summary>
    public class HeadLookController : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("プレイヤーの向きの基準（ルートオブジェクト等）")]
        public Transform playerRoot;

        [Tooltip("首・頭のボーン（視線の起点）")]
        public Transform headBone;

        [Tooltip("Rigのターゲットに設定した空オブジェクト")]
        public Transform lookTarget;

        [Tooltip("ターゲットを一元管理するマネージャー")]
        public PlayerTargetManager targetManager;

        [Header("可動域設定")]
        [Tooltip("正面から首を向ける最大角度（左右・上下）")]
        public float maxAngle = 60f;

        [Tooltip("視線移動の滑らかさ")]
        public float smoothSpeed = 5f;

        private void LateUpdate()
        {
            Vector3 targetPosition;

            if (targetManager != null && targetManager.currentTarget != null)
            {
                Transform enemy = targetManager.currentTarget;
                Vector3 dirToEnemy = enemy.position - headBone.position;

                float angleToEnemy = Vector3.Angle(playerRoot.forward, dirToEnemy);

                if (angleToEnemy <= maxAngle)
                {
                    targetPosition = enemy.position;
                }
                else
                {
                    // 可動域を超えた敵に対しては、限界角度の境界にターゲットを留める。
                    // こうしないと首が180度回転する等の不自然な見た目になる
                    Vector3 clampedDir = Vector3.RotateTowards(
                        playerRoot.forward,
                        dirToEnemy,
                        maxAngle * Mathf.Deg2Rad,
                        0f
                    );

                    // 5fは「十分遠い仮想ポイント」を作るための距離。Look Atは方向のみ参照するため値自体は重要ではない
                    targetPosition = headBone.position + clampedDir * 5f;
                }
            }
            else
            {
                targetPosition = headBone.position + playerRoot.forward * 5f;
            }

            // Lerpで補間することで、ターゲットが切り替わった瞬間に首がカクつくのを防ぐ
            lookTarget.position = Vector3.Lerp(
                lookTarget.position,
                targetPosition,
                Time.deltaTime * smoothSpeed
            );

            Debug.Log("LookAt " + lookTarget.name);
        }
    }
}
