using UnityEngine;

namespace CreativeAI.Gameplay
{
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
                // 頭から敵への方向ベクトル
                Vector3 dirToEnemy = enemy.position - headBone.position;

                // プレイヤーの正面（体の向き）と、敵への方向の角度差を計算
                float angleToEnemy = Vector3.Angle(playerRoot.forward, dirToEnemy);

                if (angleToEnemy <= maxAngle)
                {
                    // 限界角度以内なら、敵の座標をそのままターゲットにする
                    targetPosition = enemy.position;
                }
                else
                {
                    // 限界角度を超えている場合、可動域のギリギリの角度の場所にターゲットを留める
                    // RotateTowardsを使って、正面方向からmaxAngle分だけ敵の方向に傾けたベクトルを作る
                    Vector3 clampedDir = Vector3.RotateTowards(
                        playerRoot.forward,
                        dirToEnemy,
                        maxAngle * Mathf.Deg2Rad,
                        0f
                    );

                    // 頭の位置から制限された方向へ少し伸ばした位置をターゲットにする
                    targetPosition = headBone.position + clampedDir * 5f;
                }
            }
            else
            {
                // 敵がいない場合は、常に体の真正面をターゲットにする
                targetPosition = headBone.position + playerRoot.forward * 5f;
            }

            // ターゲット位置を滑らかに移動させる（急に首がカクつくのを防ぐ）
            lookTarget.position = Vector3.Lerp(
                lookTarget.position,
                targetPosition,
                Time.deltaTime * smoothSpeed
            );
        }
    }
}
