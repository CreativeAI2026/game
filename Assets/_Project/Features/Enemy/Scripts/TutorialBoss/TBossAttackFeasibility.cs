using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 周囲の地形・障害物から「今その攻撃や接近が成立するか」を判定する。
    /// 距離とクールダウンだけで攻撃を選ぶと、壁越しの空振りや段差の上への鎌振りなど、環境を無視した行動になるため切り出している。
    /// TBossPerception と同じく TutorialBossController が所有するプレーンなクラス。
    /// </summary>
    public class TBossAttackFeasibility
    {
        private readonly TutorialBossController _boss;
        private readonly TBossPerception _perception;

        // 触手の射線上のコライダーを距離順に評価するためのバッファ
        private readonly RaycastHit[] _wireHits = new RaycastHit[16];

        public TBossAttackFeasibility(TutorialBossController boss, TBossPerception perception)
        {
            _boss = boss;
            _perception = perception;
        }

        /// <summary>
        /// 近接攻撃（爪・鎌）が届く高さにプレイヤーがいるか。
        /// プレイヤーの足元がボスの腰より高い（段差の上）か、頭がボスの足元より低い（穴の下）なら届かない。
        /// 体格の比較で決めるため、ボス・プレイヤーのスケールが変わっても同じ基準になる。
        /// </summary>
        public bool IsWithinMeleeHeight()
        {
            if (_boss.PlayerCollider == null || _boss.EnemyCollider == null)
            {
                // 体格が取れない場合は高さで攻撃を封じない（従来どおり距離だけで選ばせる）
                return true;
            }

            Bounds bossBounds = _boss.EnemyCollider.bounds;
            Bounds playerBounds = _boss.PlayerCollider.bounds;
            return playerBounds.min.y <= bossBounds.center.y && playerBounds.max.y >= bossBounds.min.y;
        }

        /// <summary>
        /// 近接攻撃の振りが障害物に遮られないか。ボスの胴の高さからプレイヤーの体が見通せるかで判定する。
        /// </summary>
        public bool HasMeleeLine()
        {
            return _perception.CanSeePlayerBodyFrom(GetBossChestPosition());
        }

        /// <summary>
        /// 触手（特殊攻撃）を今の位置から撃ってプレイヤーに届くか。
        /// 触手は腕の付け根から水平にまっすぐ伸び、障害物に触れた時点で空振りになるため、同じ太さ・同じ経路で事前に掃引する。
        /// </summary>
        public bool HasClearWirePath()
        {
            if (_boss.Player == null)
            {
                return false;
            }

            Vector3 origin = GetWireOrigin();
            Vector3 direction = _boss.Player.transform.position - _boss.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }
            direction.Normalize();

            // 攻撃本体の判定（TBossSpecialAttackState.CheckHitAtTip）と同じく全レイヤー・トリガー除外で調べる
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                _boss.WireHitRadius,
                direction,
                _wireHits,
                _boss.SpecialAttackRange,
                ~0,
                QueryTriggerInteraction.Ignore
            );
            System.Array.Sort(_wireHits, 0, hitCount, HitDistanceComparer.Instance);

            for (int i = 0; i < hitCount; i++)
            {
                Transform hitTransform = _wireHits[i].transform;
                if (hitTransform.IsChildOf(_boss.transform))
                {
                    continue;
                }

                if (hitTransform.IsChildOf(_boss.Player.transform))
                {
                    return true;
                }

                if ((_boss.ObstacleLayer.value & (1 << hitTransform.gameObject.layer)) != 0)
                {
                    return false;
                }
            }

            // 射程内でプレイヤーに届かない（高さが合わず上下に外れる等）
            return false;
        }

        /// <summary>
        /// プレイヤーの近くまで、NavMesh上をまっすぐ歩いて詰められるか。
        /// 間に壁・穴・段差があると直線の間合い管理（様子見）は成立しないため、経路探索での追跡を続ける判断に使う。
        /// </summary>
        public bool HasDirectApproach()
        {
            if (_boss.Player == null || _boss.Agent == null || !_boss.Agent.isOnNavMesh)
            {
                return false;
            }

            Vector3 playerPosition = _boss.Player.transform.position;
            if (
                !NavMesh.Raycast(
                    _boss.transform.position,
                    playerPosition,
                    out NavMeshHit hit,
                    NavMesh.AllAreas
                )
            )
            {
                return true;
            }

            // NavMeshの端で止まっても、そこから通常攻撃の間合いに入れるなら直線で詰められるとみなす
            // （プレイヤーが壁際でNavMeshの外側にわずかにはみ出しているだけ、という場合を拾う）
            Vector3 gap = playerPosition - hit.position;
            gap.y = 0f;
            return gap.magnitude <= _boss.AttackRange;
        }

        private Vector3 GetBossChestPosition()
        {
            return _boss.EnemyCollider != null
                ? _boss.EnemyCollider.bounds.center
                : _boss.FlashlightTransform.position;
        }

        /// <summary>触手の付け根。TBossSpecialAttackState.ApplyWireTransform の originPos と同じ求め方をする。</summary>
        private Vector3 GetWireOrigin()
        {
            Transform wireBone = _boss.WireBone;
            if (wireBone != null && wireBone.parent != null)
            {
                return wireBone.parent.TransformPoint(_boss.InitialWireBoneLocalPosition);
            }
            return GetBossChestPosition();
        }

        private sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();

            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
