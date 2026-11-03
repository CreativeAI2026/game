using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 置きたい座標を、アイテムを実際に拾える位置へ直す。
    /// 床に合わせて埋まりを防ぎ、小物や壁のコライダーと重なる場合は近くの空いた場所へずらす。
    /// 見るのはコライダーと NavMesh だけで、「壁のどちら側(部屋の内側か)」までは判断しない。
    /// </summary>
    public static class ItemSpotFinder
    {
        /// <summary>アイテムの拾う判定(ItemPickup Prefab の SphereCollider)と同じ半径。</summary>
        public const float Radius = 0.4f;

        /// <summary>床からアイテムを浮かせる高さ。キラキラは中心が基準点で下半分が床に入りやすいので、半径ぶん以上浮かせる。</summary>
        public const float LiftAboveFloor = 0.5f;

        /// <summary>他のアイテムとの最小の間隔。</summary>
        public const float MinSeparation = 0.8f;

        private const float SearchStep = 0.5f;
        private const float MaxSearchRadius = 3f;
        private const float NavMeshSnapDistance = 1f;
        private const float FloorRayStart = 0.2f;
        private const float FloorRayLength = 1.2f;

        /// <summary>直前の <see cref="TryFind"/> が false を返したときの理由(ログ用)。</summary>
        public static string LastFailure { get; private set; }

        /// <summary>
        /// <paramref name="candidate"/> に近い順に、置ける場所を探す。見つかれば true。
        /// <paramref name="taken"/> は、すでに置いたアイテムの位置(近すぎる場所を避ける)。
        /// <paramref name="requireNavMesh"/> が true なら、歩ける場所(NavMesh 上)だけを選ぶ。高さも NavMesh から取るので、
        /// 候補の高さが床とずれていても埋まらない。NavMesh を持たない場面(単体テスト)だけ false にする。
        /// </summary>
        public static bool TryFind(
            Vector3 candidate,
            IReadOnlyCollection<Vector3> taken,
            out Vector3 spot,
            bool requireNavMesh = true
        )
        {
            Physics.SyncTransforms(); // エディタで動かした直後のコライダーの位置を反映する
            int offNavMesh = 0,
                blocked = 0,
                tooClose = 0;

            foreach (var point in Around(candidate))
            {
                if (!TryGround(point, requireNavMesh, out var ground))
                {
                    offNavMesh++;
                    continue;
                }
                if (IsBlocked(ground))
                {
                    blocked++;
                    continue;
                }
                if (IsTooClose(ground, taken))
                {
                    tooClose++;
                    continue;
                }

                spot = ground;
                return true;
            }

            LastFailure =
                $"調べた {offNavMesh + blocked + tooClose} 点のうち、NavMesh 外 {offNavMesh} / 小物や壁と重なる {blocked} / 他のアイテムに近い {tooClose}";
            spot = default;
            return false;
        }

        /// <summary>置く位置(床から少し浮かせた点)がコライダー(小物・壁)にぶつかっていないか。トリガーは無視し、床にはほぼ触れない高さで見る。</summary>
        public static bool IsBlocked(Vector3 spot) =>
            Physics.CheckSphere(
                spot + Vector3.up * (Radius + 0.25f - LiftAboveFloor), // 床から 0.25 の高さを下端に見る
                Radius,
                ~0,
                QueryTriggerInteraction.Ignore
            );

        private static bool IsTooClose(Vector3 spot, IReadOnlyCollection<Vector3> taken)
        {
            if (taken == null)
                return false;
            foreach (var other in taken)
                if ((other - spot).sqrMagnitude < MinSeparation * MinSeparation)
                    return true;
            return false;
        }

        /// <summary>候補そのものを先頭に、周りへ広がる順に点を返す。</summary>
        private static IEnumerable<Vector3> Around(Vector3 center)
        {
            yield return center;
            for (var radius = SearchStep; radius <= MaxSearchRadius; radius += SearchStep)
            {
                var count = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * radius / SearchStep));
                for (var i = 0; i < count; i++)
                {
                    var angle = 2f * Mathf.PI * i / count;
                    yield return center
                        + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                }
            }
        }

        /// <summary>
        /// NavMesh に寄せ、その真下の床の高さに合わせる。
        /// <paramref name="requireNavMesh"/> が true で近くに NavMesh が無ければ false(その点は使えない)。
        /// </summary>
        private static bool TryGround(Vector3 point, bool requireNavMesh, out Vector3 ground)
        {
            var start = point;
            if (NavMesh.SamplePosition(point, out var hit, NavMeshSnapDistance, NavMesh.AllAreas))
                start = hit.position;
            else if (requireNavMesh)
            {
                ground = default;
                return false;
            }

            var origin = start + Vector3.up * FloorRayStart;
            if (
                Physics.Raycast(
                    origin,
                    Vector3.down,
                    out var floor,
                    FloorRayLength,
                    ~0,
                    QueryTriggerInteraction.Ignore
                )
            )
                start = floor.point;

            ground = start + Vector3.up * LiftAboveFloor;
            return true;
        }
    }
}
