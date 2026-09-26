using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// TutorialBoss の知覚（懐中電灯での視認判定・発見度 Awareness の管理・直近のプレイヤー位置の記憶）を担当する。
    /// Prefab の再構成や参照破損を避けるため MonoBehaviour にせず、TutorialBossController が所有するプレーンなクラスにしている。
    /// </summary>
    public class TBossPerception
    {
        private readonly TutorialBossController _boss;

        // Awarenessの上昇・減衰速度（/秒）。光の中心に近いほど速く気づく。
        private const float AwarenessRiseRateAtEdge = 20f;
        private const float AwarenessRiseRateAtCenter = 60f;
        private const float AwarenessDecayRate = 25f;
        private const float AwarenessMax = 100f;

        /// <summary>この値を超えたら「怪しい」と感じ、Patrol中に一度立ち止まって振り返る。</summary>
        public const float SuspiciousThreshold = 40f;

        /// <summary>0〜100。100に達すると完全発見（IsFullyAware）とみなす。</summary>
        public float Awareness { get; private set; }

        public bool IsFullyAware => Awareness >= AwarenessMax;
        public bool IsSuspicious => Awareness >= SuspiciousThreshold;

        /// <summary>最後にプレイヤーの位置を把握した座標（視認・被弾などから更新）。</summary>
        public Vector3 LastKnownPlayerPosition { get; private set; }

        /// <summary>一度でもプレイヤーの位置を把握したことがあるか。falseの間はLastKnownPlayerPositionが無効。</summary>
        public bool HasKnownPlayerPosition { get; private set; }

        public TBossPerception(TutorialBossController boss)
        {
            _boss = boss;
        }

        /// <summary>
        /// 毎フレーム呼び出す。視認判定の結果に応じてAwarenessと既知位置を更新する。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_boss.Player == null)
            {
                return;
            }

            bool inSight = IsPlayerVisible(out float centerAlignment);

            if (inSight)
            {
                LastKnownPlayerPosition = _boss.Player.transform.position;
                HasKnownPlayerPosition = true;

                float riseRate = Mathf.Lerp(
                    AwarenessRiseRateAtEdge,
                    AwarenessRiseRateAtCenter,
                    centerAlignment
                );
                Awareness = Mathf.Min(AwarenessMax, Awareness + riseRate * deltaTime);
            }
            else
            {
                Awareness = Mathf.Max(0f, Awareness - AwarenessDecayRate * deltaTime);
            }
        }

        /// <summary>
        /// 音を検知した際にAwarenessへ反映する。confidenceは0〜1（音源に近いほど高い）。
        /// 音だけでは完全発見はさせず、疑いを強める程度に留める。
        /// </summary>
        public void NotifySoundHeard(float confidence)
        {
            float awarenessFromSound = confidence * SuspiciousThreshold;
            Awareness = Mathf.Max(Awareness, awarenessFromSound);
        }

        /// <summary>
        /// 視認以外（被弾など）でプレイヤーの推定位置を把握した際に呼ぶ。即座に完全発見状態にする。
        /// </summary>
        public void NotifyDamageFrom(Vector3 estimatedPosition)
        {
            LastKnownPlayerPosition = estimatedPosition;
            HasKnownPlayerPosition = true;
            Awareness = AwarenessMax;
        }

        /// <summary>
        /// プレイヤーが懐中電灯の光円錐内にいるかどうかの判定（外部公開用、centerAlignment不要な呼び出し向け）。
        /// </summary>
        public bool IsPlayerVisible()
        {
            return IsPlayerVisible(out _);
        }

        /// <summary>
        /// プレイヤーが懐中電灯の光円錐（SpotAngle・Range）内にいるかを判定する。障害物遮蔽も考慮する。
        /// centerAlignmentは0（光の端）〜1（光の中心）。Awarenessの上昇速度計算に使う。
        /// </summary>
        private bool IsPlayerVisible(out float centerAlignment)
        {
            centerAlignment = 0f;

            Transform flashlightTransform = _boss.FlashlightTransform;
            Light flashlightLight = _boss.FlashlightLight;

            if (_boss.Player == null || flashlightLight == null || flashlightTransform == null)
            {
                return false;
            }

            // 距離・角度・遮蔽の3判定すべてで同じ基準点（胸元）を使う。
            // 基準点が判定ごとに違うと、近距離ほど角度判定だけがずれて
            // 「光の中心にいるのに発見されない」という不整合が起きるため統一する。
            Vector3 playerRefPoint = _boss.Player.transform.position + Vector3.up * 1f;
            Vector3 toPlayer = playerRefPoint - flashlightTransform.position;
            float distance = toPlayer.magnitude;

            if (distance > flashlightLight.range)
            {
                return false;
            }

            float halfAngle = flashlightLight.spotAngle * 0.5f;
            float angle = Vector3.Angle(flashlightTransform.forward, toPlayer.normalized);
            if (angle > halfAngle)
            {
                return false;
            }

            centerAlignment = halfAngle > 0f ? 1f - Mathf.Clamp01(angle / halfAngle) : 1f;

            Vector3 rayDir = toPlayer.normalized;
            if (
                Physics.Raycast(
                    flashlightTransform.position,
                    rayDir,
                    out RaycastHit hit,
                    distance,
                    _boss.ObstacleLayer
                )
            )
            {
                return false;
            }

            return true;
        }
    }
}
