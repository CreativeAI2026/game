using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 近接攻撃の当たりの検出。前フレームからの軌跡を SphereCast で、密着・めり込みを OverlapSphere で拾い、
    /// まだ当てていないコライダーごとに当たったときの処理を呼ぶ。当たった扱いにするかは呼ばれた側が MarkHit で決める。
    /// </summary>
    public sealed class MeleeSweep
    {
        /// <summary>コライダーに触れたときの処理。引数は相手・当たった位置・当たった面の外向き。</summary>
        public delegate void HitHandler(Collider target, Vector3 point, Vector3 normal);

        private readonly HashSet<Collider> _alreadyHitTargets = new HashSet<Collider>();

        // 物理クエリ結果の再利用バッファ(判定するレイヤーが広い場合も取りこぼさないよう大きめ)
        private readonly RaycastHit[] _castHits = new RaycastHit[64];
        private readonly Collider[] _overlapHits = new Collider[64];

        private Vector3 _previousPosition;

        /// <summary>判定が有効か。</summary>
        public bool IsActive { get; private set; }

        /// <summary>直近で判定が動いた向き(= 振った向き)。まだ動いていなければ zero。</summary>
        public Vector3 SwingDirection { get; private set; }

        /// <summary>判定を始める。軌跡の起点を記録し、当てた相手の記録を消す。</summary>
        public void Begin(Vector3 position)
        {
            _alreadyHitTargets.Clear();
            // 軌跡判定の起点を記録し、最初のフレームで正しくSphereCastできるようにする
            _previousPosition = position;
            SwingDirection = Vector3.zero;
            IsActive = true;
        }

        /// <summary>判定を終える。</summary>
        public void End()
        {
            IsActive = false;
            _alreadyHitTargets.Clear();
        }

        /// <summary>この判定中は同じ相手にもう当てないようにする(1 回の振りにつき 1 回だけ当てる)。</summary>
        public void MarkHit(Collider target)
        {
            _alreadyHitTargets.Add(target);
        }

        /// <summary>前回の位置から今の位置までを判定し、触れたコライダーごとに当たったときの処理を呼ぶ。</summary>
        public void Sweep(
            Vector3 currentPosition,
            float radius,
            LayerMask targetLayer,
            HitHandler onHit
        )
        {
            if (!IsActive)
                return;

            Vector3 direction = currentPosition - _previousPosition;
            float distance = direction.magnitude;

            // 高速スイング時に武器がターゲットをすり抜けないよう、
            // 前フレームから現フレームまでの軌跡全体を太いレイで判定する
            if (distance > 0)
            {
                SwingDirection = direction / distance;
                int hitCount = Physics.SphereCastNonAlloc(
                    _previousPosition,
                    radius,
                    SwingDirection,
                    _castHits,
                    distance,
                    targetLayer
                );
                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit hit = _castHits[i];
                    // SphereCastAll は始点でコライダーと既に重なっている場合
                    // hit.point に Vector3.zero を返す仕様上の問題があるため、
                    // その場合はClosestPointで代替する
                    Vector3 resolvedPoint =
                        (hit.point == Vector3.zero)
                            ? hit.collider.ClosestPoint(currentPosition)
                            : hit.point;
                    Notify(hit.collider, resolvedPoint, hit.normal, onHit);
                }
            }

            // SphereCastは始点内部の重なりを検出できないため、
            // 密着・めり込み状態をOverlapSphereで補完する
            int overlapCount = Physics.OverlapSphereNonAlloc(
                currentPosition,
                radius,
                _overlapHits,
                targetLayer
            );
            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = _overlapHits[i];
                // OverlapSphereは接触点を返さないため、エフェクト表示用にClosestPointで近似する
                Vector3 hitPoint = col.ClosestPoint(currentPosition);
                Vector3 hitNormal = (currentPosition - hitPoint).normalized;
                Notify(col, hitPoint, hitNormal, onHit);
            }

            _previousPosition = currentPosition;
        }

        /// <summary>判定の範囲をシーンビューに描く。</summary>
        public static void DrawGizmo(Vector3 position, float radius)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawSphere(position, radius);
        }

        private void Notify(Collider target, Vector3 point, Vector3 normal, HitHandler onHit)
        {
            if (_alreadyHitTargets.Contains(target))
                return;
            onHit(target, point, normal);
        }
    }
}
