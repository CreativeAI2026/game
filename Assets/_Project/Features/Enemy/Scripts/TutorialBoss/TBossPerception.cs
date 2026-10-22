using UnityEngine;
using UnityEngine.AI;

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

        // プレイヤーの体のどの高さを視認判定に使うか（コライダー高さに対する割合。頭・胸・腰・足元）。
        // 懐中電灯の光円錐は近距離ほど縦に細くなるため、1点だけだと「光が体に当たっているのに判定外」になる。
        // 体格（スケール）が変わっても同じ部位を指すよう、固定メートルではなく割合で持つ。
        private static readonly float[] SampleHeightRatios = { 0.9f, 0.65f, 0.4f, 0.1f };

        // 遮蔽判定で自分自身・プレイヤーの子コライダーを除外するため、複数ヒットを受け取るバッファ。
        private readonly RaycastHit[] _losHits = new RaycastHit[8];
        private readonly Vector3[] _samplePoints = new Vector3[SampleHeightRatios.Length];

        // 同一フレームに Tick と各ステートから何度も呼ばれるため、視認結果をフレーム単位でキャッシュする。
        private int _cachedFrame = -1;
        private bool _cachedVisible;
        private float _cachedCenterAlignment;

        /// <summary>0〜100。100に達すると完全発見（IsFullyAware）とみなす。</summary>
        public float Awareness { get; private set; }

        public bool IsFullyAware => Awareness >= AwarenessMax;
        public bool IsSuspicious => Awareness >= SuspiciousThreshold;

        /// <summary>最後にプレイヤーの位置を把握した座標（視認・被弾などから更新）。</summary>
        public Vector3 LastKnownPlayerPosition { get; private set; }

        /// <summary>一度でもプレイヤーの位置を把握したことがあるか。falseの間はLastKnownPlayerPositionが無効。</summary>
        public bool HasKnownPlayerPosition { get; private set; }

        /// <summary>最後に視認した瞬間のプレイヤーの水平移動速度。見失った後の逃げた先の予測に使う。</summary>
        public Vector3 LastKnownPlayerVelocity { get; private set; }

        // 見失った後、逃げた方向へ何秒分先を予測して捜索するか。
        // 長くすると曲がったプレイヤーから大きく外れるため、直進を見込める程度の短さにする
        private const float PredictionSeconds = 1f;

        // 予測地点をNavMesh上へ補正する際の探索半径（ボスのスケール1換算）
        private const float PredictionSampleRadius = 2f;

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

            if (_boss.IsPlayerDead)
            {
                // 倒したプレイヤーを発見し続けて攻撃に戻らないよう、発見度を上げない
                Awareness = 0f;
                return;
            }

            bool inSight = IsPlayerVisible(out float centerAlignment);

            if (inSight)
            {
                RememberPlayerPosition();

                float riseRate = Mathf.Lerp(
                    AwarenessRiseRateAtEdge,
                    AwarenessRiseRateAtCenter,
                    centerAlignment
                );
                Awareness = Mathf.Min(AwarenessMax, Awareness + riseRate * deltaTime);
            }
            else if (_boss.IsAlerted && IsPlayerWithinCloseSense())
            {
                // 交戦中に至近距離で光から外れただけなら、存在は把握し続ける（発見度も下げない）
                RememberPlayerPosition();
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
        /// プレイヤー自身の位置で鳴る音（足音・ハンマー）を聞いた際に、既知位置を音源へ更新する。
        /// 視認ではないため移動速度は更新しない。
        /// </summary>
        public void NotifyPlayerHeardAt(Vector3 position)
        {
            LastKnownPlayerPosition = position;
            HasKnownPlayerPosition = true;
        }

        /// <summary>プレイヤーの把握状況をすべて忘れる。プレイヤーを倒して交戦を終える際に使う。</summary>
        public void ForgetPlayer()
        {
            Awareness = 0f;
            HasKnownPlayerPosition = false;
            LastKnownPlayerVelocity = Vector3.zero;
        }

        /// <summary>
        /// その音がプレイヤーの居場所で鳴るものか。矢の着弾音は着弾点で鳴る（囮に使える）ため、プレイヤーの位置として扱わない。
        /// </summary>
        public static bool IsEmittedAtPlayer(SoundType type)
        {
            return type != SoundType.ArrowHit;
        }

        /// <summary>
        /// 見失ったプレイヤーが逃げた先の予測地点。最後に見た位置から、最後に見た移動方向へ少し先を読む。
        /// 壁を突き抜けた地点を予測しないよう、NavMesh上で最後に見た位置から直線で行ける所までに留める。
        /// </summary>
        public Vector3 PredictPlayerPosition()
        {
            Vector3 origin = LastKnownPlayerPosition;
            float sampleRadius = PredictionSampleRadius * _boss.BodyScale;
            if (
                !NavMesh.SamplePosition(
                    origin,
                    out NavMeshHit originHit,
                    sampleRadius,
                    NavMesh.AllAreas
                )
            )
            {
                return origin;
            }

            Vector3 predicted = originHit.position + LastKnownPlayerVelocity * PredictionSeconds;
            if (
                NavMesh.Raycast(
                    originHit.position,
                    predicted,
                    out NavMeshHit wallHit,
                    NavMesh.AllAreas
                )
            )
            {
                return wallHit.position;
            }
            return predicted;
        }

        /// <summary>
        /// プレイヤーが懐中電灯の光円錐内にいるかどうかの判定（外部公開用、centerAlignment不要な呼び出し向け）。
        /// </summary>
        public bool IsPlayerVisible()
        {
            return IsPlayerVisible(out _);
        }

        /// <summary>
        /// 交戦中（Chase・Watch）用の捕捉判定。光円錐内に加え、至近距離で遮蔽物が無ければ光の外でも捕捉し続ける。
        /// 光円錐は細いため、これが無いと横に一歩ずれただけで見失い、目の前のプレイヤーを捜索し始めてしまう。
        /// </summary>
        public bool IsPlayerTracked()
        {
            return IsPlayerVisible() || IsPlayerWithinCloseSense();
        }

        private void RememberPlayerPosition()
        {
            LastKnownPlayerPosition = _boss.Player.transform.position;
            HasKnownPlayerPosition = true;

            if (_boss.PlayerCollider is CharacterController playerCC)
            {
                Vector3 velocity = playerCC.velocity;
                velocity.y = 0f;
                LastKnownPlayerVelocity = velocity;
            }
        }

        /// <summary>
        /// プレイヤーが懐中電灯の光円錐（SpotAngle・Range）内にいるかを判定する。障害物遮蔽も考慮する。
        /// 体の複数の高さを調べ、どこか1点でも光に入って見えていれば視認とする。
        /// centerAlignmentは0（光の端）〜1（光の中心）で、見えている点のうち最も中心に近い値。Awarenessの上昇速度計算に使う。
        /// </summary>
        private bool IsPlayerVisible(out float centerAlignment)
        {
            if (_cachedFrame == Time.frameCount)
            {
                centerAlignment = _cachedCenterAlignment;
                return _cachedVisible;
            }

            _cachedVisible = EvaluateFlashlightVisibility(out _cachedCenterAlignment);
            _cachedFrame = Time.frameCount;
            centerAlignment = _cachedCenterAlignment;
            return _cachedVisible;
        }

        private bool EvaluateFlashlightVisibility(out float centerAlignment)
        {
            centerAlignment = 0f;

            Transform flashlightTransform = _boss.FlashlightTransform;
            Light flashlightLight = _boss.FlashlightLight;

            if (
                _boss.Player == null
                || _boss.IsPlayerDead
                || flashlightLight == null
                || flashlightTransform == null
            )
            {
                return false;
            }

            Vector3 origin = flashlightTransform.position;
            float halfAngle = flashlightLight.spotAngle * 0.5f;
            bool visible = false;

            int count = FillPlayerSamplePoints();
            for (int i = 0; i < count; i++)
            {
                Vector3 toPoint = _samplePoints[i] - origin;
                if (toPoint.magnitude > flashlightLight.range)
                {
                    continue;
                }

                float angle = Vector3.Angle(flashlightTransform.forward, toPoint);
                if (angle > halfAngle)
                {
                    continue;
                }

                float alignment = halfAngle > 0f ? 1f - Mathf.Clamp01(angle / halfAngle) : 1f;
                if (alignment <= centerAlignment && visible)
                {
                    // より中心寄りの点で既に視認できているので、遮蔽判定を省く
                    continue;
                }

                if (HasLineOfSight(origin, _samplePoints[i]))
                {
                    visible = true;
                    centerAlignment = alignment;
                }
            }

            return visible;
        }

        /// <summary>
        /// 光円錐に関係なく、交戦距離（CombatCloseSenseRadius）以内で遮蔽物なく見通せるかを判定する。
        /// </summary>
        private bool IsPlayerWithinCloseSense()
        {
            if (_boss.Player == null || _boss.IsPlayerDead || _boss.FlashlightTransform == null)
            {
                return false;
            }

            Vector3 toPlayer = _boss.Player.transform.position - _boss.transform.position;
            toPlayer.y = 0f;
            if (toPlayer.magnitude > _boss.CombatCloseSenseRadius)
            {
                return false;
            }

            return CanSeePlayerBodyFrom(_boss.FlashlightTransform.position);
        }

        /// <summary>
        /// 指定位置から、プレイヤーの体（頭・胸・腰・足元）のどこか1点でも遮蔽物なく見通せるか。光円錐は考慮しない。
        /// 知覚だけでなく、攻撃が障害物に阻まれないかの判定（TBossAttackFeasibility）にも使う。
        /// </summary>
        public bool CanSeePlayerBodyFrom(Vector3 origin)
        {
            if (_boss.Player == null)
            {
                return false;
            }

            int count = FillPlayerSamplePoints();
            for (int i = 0; i < count; i++)
            {
                if (HasLineOfSight(origin, _samplePoints[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// プレイヤーのコライダー（CharacterController）の高さから、判定に使う体の各点を求める。
        /// </summary>
        private int FillPlayerSamplePoints()
        {
            Collider playerCollider = _boss.PlayerCollider;
            Vector3 bottom;
            float height;

            if (playerCollider != null)
            {
                Bounds bounds = playerCollider.bounds;
                bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                height = bounds.size.y;
            }
            else
            {
                // コライダーが取れない場合は、足元からスケール1換算で1m上（従来の胸元相当）のみを使う
                Transform playerTransform = _boss.Player.transform;
                _samplePoints[0] =
                    playerTransform.position + Vector3.up * playerTransform.lossyScale.y;
                return 1;
            }

            for (int i = 0; i < SampleHeightRatios.Length; i++)
            {
                _samplePoints[i] = bottom + Vector3.up * (height * SampleHeightRatios[i]);
            }
            return SampleHeightRatios.Length;
        }

        /// <summary>
        /// origin から target までに障害物が無いか。ボス自身・プレイヤーの子コライダーとトリガーは遮蔽物として扱わない。
        /// </summary>
        private bool HasLineOfSight(Vector3 origin, Vector3 target)
        {
            Vector3 toTarget = target - origin;
            float distance = toTarget.magnitude;
            if (distance < 0.0001f)
            {
                return true;
            }

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                toTarget / distance,
                _losHits,
                distance,
                _boss.ObstacleLayer,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hitCount; i++)
            {
                Transform hitTransform = _losHits[i].transform;
                if (
                    hitTransform.IsChildOf(_boss.transform)
                    || hitTransform.IsChildOf(_boss.Player.transform)
                )
                {
                    continue;
                }
                return false;
            }
            return true;
        }
    }
}
