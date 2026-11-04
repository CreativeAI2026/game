using UnityEngine;

namespace CreativeAI.VisualTeam
{
    /// <summary>
    /// カメラから見た敵の状況を、画像ではなく幾何計算（視錐台・ビューポート座標・Linecast）で求めて、
    /// Sight に書き込む。
    /// </summary>
    public class Observer : MonoBehaviour
    {
        [SerializeField] private Camera source;
        [SerializeField] private Sight target;

        // 観測する敵のコライダー（敵1体につき、全体を覆うもの1つ）
        [SerializeField] private Collider[] enemies;
        // 視線を遮るレイヤー（壁など）。敵自身に当たった場合は遮蔽とみなさない
        [SerializeField] private LayerMask occluders;

        [SerializeField] private bool log;

        private readonly Plane[] planes = new Plane[6];

        void Update()
        {
            Observe();
        }

        /// <summary>現在のカメラから見た状況を計算して、this.target に書き込む（記録の直前に1回呼ぶ）</summary>
        public void Observe()
        {
            if (this.target == null || this.source == null) return;

            // カメラの射影行列と位置・向きから、視錐台の6平面（左・右・下・上・手前・奥）を求める。
            // 配列を渡すオーバーロードなので、呼ぶたびのメモリ確保はない
            GeometryUtility.CalculateFrustumPlanes(this.source, this.planes);

            Vector3 origin = this.source.transform.position;

            int inView = 0, visible = 0;
            float best = float.MaxValue;
            Bounds nearest = default;

            for (int index = 0; index < this.enemies.Length; ++index)
            {
                Collider e = this.enemies[index];

                if (e == null || !e.enabled || !e.gameObject.activeInHierarchy) continue;

                Bounds b = e.bounds;

                // 1) 視錐台に入っているか（壁越しも含む）
                if (!GeometryUtility.TestPlanesAABB(this.planes, b)) continue;
                inView++;

                // 2) 視線が壁に遮られていないか
                if (this.IsOccluded(origin, e, b.center)) continue;
                visible++;

                // 3) 見えている敵のうち、最も近いもの
                float d = Vector3.Distance(origin, b.center);

                if (d < best)
                {
                    best = d;
                    nearest = b;
                }
            }

            this.target.enemy_in_view = inView > 0 ? 1f : 0f;
            this.target.enemy_visible = visible > 0 ? 1f : 0f;
            this.target.enemy_count = visible;

            if (visible == 0)
            {
                this.target.enemy_distance = -1f;
                this.target.enemy_angle = -1f;
                this.target.enemy_screen_x = 0f;
                this.target.enemy_screen_y = 0f;
                this.target.enemy_area = 0f;
                return;
            }

            // ビューポート座標: 画面左下が(0,0)、右上が(1,1)。zはカメラからの距離
            Vector3 vp = this.source.WorldToViewportPoint(nearest.center);

            this.target.enemy_distance = best;
            this.target.enemy_angle = Vector3.Angle(this.source.transform.forward, nearest.center - origin);
            this.target.enemy_screen_x = vp.x * 2f - 1f;
            this.target.enemy_screen_y = vp.y * 2f - 1f;
            this.target.enemy_area = this.ScreenArea(nearest);
        }

        /// <summary>カメラ位置から敵の中心へ線を引き、最初に当たったものが敵自身（またはその子）でなければ遮蔽されている</summary>
        private bool IsOccluded(Vector3 origin, Collider target, Vector3 to)
        {
            if (!Physics.Linecast(origin, to, out RaycastHit hit, this.occluders, QueryTriggerInteraction.Ignore))
                return false;

            bool isSelf = hit.collider == target || hit.collider.transform.IsChildOf(target.transform);

            return !isSelf;
        }

        /// <summary>バウンディングボックスの8頂点を画面に投影した、外接矩形の面積（画面全体を1とする）</summary>
        private float ScreenArea(Bounds b)
        {
            Vector3 c = b.center, e = b.extents;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            for (int index = 0; index < 8; ++index)
            {
                var corner = c + new Vector3(
                    (index & 1) == 0 ? -e.x : e.x,
                    (index & 2) == 0 ? -e.y : e.y,
                    (index & 4) == 0 ? -e.z : e.z);

                Vector3 v = this.source.WorldToViewportPoint(corner);

                if (v.z <= 0f) continue; // カメラの後ろ

                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }

            if (maxX < minX) return 0f;

            // 画面外にはみ出した分は切る
            minX = Mathf.Clamp01(minX); maxX = Mathf.Clamp01(maxX);
            minY = Mathf.Clamp01(minY); maxY = Mathf.Clamp01(maxY);

            return (maxX - minX) * (maxY - minY);
        }
    }
}
