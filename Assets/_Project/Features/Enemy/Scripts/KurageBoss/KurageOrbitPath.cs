using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスが回るステージ外周の周回路。ステージのコライダーの上面を、辺から一定距離だけ外へ広げた角丸の四角形にする。
    /// 角では同じ距離を保ったまま円弧で回り込むので、角に来ても止まらず膨らんで移動し続けられる。
    ///
    /// 位置は「周回路に沿った距離 s(0〜Length)」で表す。s が増える向きは上から見て反時計回り。
    /// 正式なステージができたら、そのコライダーを割り当てれば同じ仕組みで回れる。
    /// </summary>
    public class KurageOrbitPath : MonoBehaviour
    {
        private const int CornerCount = 4;

        [Tooltip("周りを回る高台。上面の高さと広さをこのコライダーの範囲から取る")]
        [SerializeField]
        private Collider _stage;

        [Tooltip("ステージの辺から周回路までの水平距離（m）。角の円弧の半径にもなる")]
        [SerializeField]
        private float _distanceFromEdge = 10f;

        [Tooltip("ステージ上面からの高さ（m）。クラゲの原点（モデルの足元）の高さ")]
        [SerializeField]
        private float _heightAboveStageTop = 0.8f;

        // 角の中心(ステージ上面の四隅)。反時計回りに (+x,+z) (-x,+z) (-x,-z) (+x,-z) の順
        private readonly Vector3[] _cornerCenters = new Vector3[CornerCount];
        private float _edgeLengthX;
        private float _edgeLengthZ;
        private float _arcLength;

        /// <summary>周回路の1周の長さ（m）。</summary>
        public float Length { get; private set; }

        /// <summary>ステージ上面の高さ（ワールドY）。</summary>
        public float StageTopY { get; private set; }

        /// <summary>ステージ上面の範囲（ワールド座標の AABB）。地面に出す攻撃の位置を収めるのに使う。</summary>
        public Bounds StageBounds => _stage.bounds;

        private void Awake()
        {
            Assert.IsNotNull(_stage, $"{name}: 周回するステージ(Collider)が設定されていません");
            Rebuild();
        }

        /// <summary>ステージの範囲から周回路を作り直す。ステージを動かした・広さを変えた時に呼ぶ。</summary>
        public void Rebuild()
        {
            Bounds b = _stage.bounds;
            StageTopY = b.max.y;
            float y = StageTopY + _heightAboveStageTop;
            float hx = b.extents.x;
            float hz = b.extents.z;
            Vector3 c = b.center;

            _cornerCenters[0] = new Vector3(c.x + hx, y, c.z + hz);
            _cornerCenters[1] = new Vector3(c.x - hx, y, c.z + hz);
            _cornerCenters[2] = new Vector3(c.x - hx, y, c.z - hz);
            _cornerCenters[3] = new Vector3(c.x + hx, y, c.z - hz);

            _edgeLengthX = hx * 2f;
            _edgeLengthZ = hz * 2f;
            _arcLength = _distanceFromEdge * Mathf.PI * 0.5f;
            Length = (_edgeLengthX + _edgeLengthZ) * 2f + _arcLength * CornerCount;
        }

        /// <summary>s の位置を返す。outward は周回路の外向き（ステージから離れる向き）の水平単位ベクトル。</summary>
        public Vector3 Evaluate(float s, out Vector3 outward)
        {
            s = Mathf.Repeat(s, Length);

            // 辺 k → 角 k の順に並ぶ。辺 k は角 k-1 から角 k までで、外向きは角 k の円弧の開始角(90°×k)の向き
            for (int k = 0; k < CornerCount; k++)
            {
                float startAngle = 90f * k;
                Vector3 edgeNormal = Direction(startAngle);
                float edgeLength = (k % 2 == 0) ? _edgeLengthZ : _edgeLengthX;

                if (s <= edgeLength)
                {
                    Vector3 from = _cornerCenters[(k + CornerCount - 1) % CornerCount];
                    Vector3 to = _cornerCenters[k];
                    outward = edgeNormal;
                    return Vector3.Lerp(from, to, s / edgeLength) + edgeNormal * _distanceFromEdge;
                }
                s -= edgeLength;

                if (s <= _arcLength)
                {
                    float angle = startAngle + 90f * (s / _arcLength);
                    outward = Direction(angle);
                    return _cornerCenters[k] + outward * _distanceFromEdge;
                }
                s -= _arcLength;
            }

            // 浮動小数の誤差で1周ぶん引き切れなかった場合は始点(辺0の始め)に丸める
            outward = Direction(0f);
            return _cornerCenters[CornerCount - 1] + outward * _distanceFromEdge;
        }

        public Vector3 Evaluate(float s) => Evaluate(s, out _);

        /// <summary>
        /// worldPoint に水平距離で最も近い周回路上の s を返す。プレイヤーに近づく・回り込む向きを決めるのに使う。
        /// 周回路は単純な形なので、細かく刻んで探す。
        /// </summary>
        public float FindClosestS(Vector3 worldPoint, float step = 0.5f)
        {
            float bestS = 0f;
            float bestDistance = float.MaxValue;
            for (float s = 0f; s < Length; s += step)
            {
                Vector3 p = Evaluate(s);
                float dx = p.x - worldPoint.x;
                float dz = p.z - worldPoint.z;
                float d = dx * dx + dz * dz;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    bestS = s;
                }
            }
            return bestS;
        }

        /// <summary>from から to へ周回路を最短で進む時の符号付きの距離（正なら s が増える向き）。</summary>
        public float SignedDistance(float from, float to)
        {
            float d = Mathf.Repeat(to - from, Length);
            return d > Length * 0.5f ? d - Length : d;
        }

        private static Vector3 Direction(float angleDegrees)
        {
            // 上から見て +X から +Z へ回る角度
            float rad = angleDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
        }

        private void OnDrawGizmos()
        {
            if (_stage == null)
                return;

            // 編集中のシーンでも周回路が見えるよう、描くたびに作り直す
            Rebuild();
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
            const int Segments = 160;
            Vector3 prev = Evaluate(0f);
            for (int i = 1; i <= Segments; i++)
            {
                Vector3 next = Evaluate(Length * i / Segments);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
