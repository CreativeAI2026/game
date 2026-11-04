using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Core
{
    /// <summary>
    /// プレイヤーからマップピンまでの道筋を、地面に沿って矢じりを並べた線で描く。
    /// MapPin が持ち、Tick を毎フレーム呼ぶ。経路の計算は一定間隔でだけ行い、その間は
    /// 前の形から新しい形へ少しずつ近づけて、線がカクつかないようにする。
    /// 経路が無い(NavMesh が途切れている、別の階など)・近づいた・プレイヤーが居ないときは線を消す。
    /// </summary>
    public sealed class MapPinPathLine
    {
        public const string MaterialResource = "MapPinGroundLine";
        private const float RefreshSeconds = 0.3f;
        private const float HideWithin = 2.5f; // この近さまで来たら消す
        private const float SnapDistance = 3f; // ピンの真下の NavMesh を探す範囲
        private const float LiftAboveFloor = 0.15f; // 床に埋まらないよう少し浮かせる
        private const float Width = 0.9f; // 矢じり1つの幅
        private const float ArrowSpacing = 1.4f; // 矢じりの間隔(ワールド長)
        private const float FlowSpeed = 0.9f; // 矢じりが進む速さ(ワールド長/秒)
        private const float PointSpacing = 0.3f; // 線の点の間隔(曲線のなめらかさ)
        private const int StairSmoothPasses = 2; // 階段で線を段の角より上に持ち上げる回数
        private const float BendRadius = 4f; // 曲がり角を丸める大きさ
        private const int BendSteps = 10; // 丸めた角の細かさ
        private const int BendAttempts = 4; // 丸め方が通れないとき、大きさを半分にして試す回数
        private const float ObstacleCheckHeight = 0.5f; // 物との当たりを調べる、床からの高さ
        private const float FollowSharpness = 14f; // 新しい形へ近づく速さ(大きいほど速い)
        private const float NearSnapRadius = 5f; // この近さの点は毎フレーム床に合わせ直す
        private const float VerticalTolerance = 1f; // 線の高さを取る NavMesh を探す範囲(階の高さより十分小さく)
        private const float OnPathTolerance = 0.35f; // 直線の途中の点が NavMesh からずれてよい範囲
        private const int MaxStraightSamples = 20; // 直線の確認で調べる点の数の上限(重くしないため)
        private static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");

        private readonly LineRenderer _line;
        private readonly NavMeshPath _path = new();
        private readonly MaterialPropertyBlock _block = new();
        private readonly List<Vector3> _goal = new(); // 計算で求めた最新の道筋(目標の形)
        private readonly List<Vector3> _shown = new(); // 実際に描いている道筋(目標へ近づけていく)
        private readonly List<Vector3> _dense = new();
        private Transform _player;
        private float _nextRefresh;

        public MapPinPathLine()
        {
            // 線は世界座標で描く。回転するピンの子にはしない(線の向きが回ってしまうため)。
            var go = new GameObject("MapPinPathLine");
            // 線の面を上へ向ける(TransformZ は Z 軸方向に面を向ける)。
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            _line = go.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.alignment = LineAlignment.TransformZ;
            _line.widthMultiplier = Width;
            _line.numCornerVertices = 2;
            // 矢じりの画像を道筋に沿って並べる(画像の向き=進む向き)。
            _line.textureMode = LineTextureMode.Tile;
            _line.textureScale = new Vector2(1f / ArrowSpacing, 1f);
            _line.colorGradient = FadeGradient();
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.allowOcclusionWhenDynamic = false;
            var material = Resources.Load<Material>(MaterialResource);
            if (material != null)
                _line.sharedMaterial = material;
            else
                Debug.LogWarning($"[MapPin] Resources/{MaterialResource} が見つかりません。");
            _line.enabled = false;
        }

        public bool Visible => _line != null && _line.enabled;

        public void Tick(Vector3 target)
        {
            if (_line == null)
                return;
            if (Time.time >= _nextRefresh)
            {
                _nextRefresh = Time.time + RefreshSeconds;
                Refresh(target);
            }
            if (_line.enabled)
            {
                FollowGoal();
                Flow();
            }
        }

        // 手前と奥を薄くして、線の出だしと終わりが急に切れて見えないようにする。
        private static Gradient FadeGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(1f, 0.8f),
                    new GradientAlphaKey(0f, 1f),
                }
            );
            return gradient;
        }

        private void Refresh(Vector3 target)
        {
            if (_player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                _player = go != null ? go.transform : null;
            }
            if (_player == null)
            {
                Hide();
                return;
            }

            var from = _player.position;
            if (Vector3.Distance(from, target) < HideWithin)
            {
                Hide();
                return;
            }

            if (
                !NavMesh.SamplePosition(from, out var start, SnapDistance, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(target, out var goal, SnapDistance, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(start.position, goal.position, NavMesh.AllAreas, _path)
                || _path.status != NavMeshPathStatus.PathComplete
                || _path.corners.Length < 2
            )
            {
                Hide();
                return;
            }

            BuildGoal(_path.corners);
            if (!_line.enabled)
            {
                // 出始めは目標の形をそのまま使う(遠くから縮んでくるのを避ける)。
                _shown.Clear();
                _shown.AddRange(_goal);
                _line.enabled = true;
            }
        }

        private void Hide()
        {
            _line.enabled = false;
            _goal.Clear();
            _shown.Clear();
        }

        // NavMesh の経路は「最短」ではなく「コストが最小」の道なので、真っすぐ歩ける所でも遠回りすることがある。
        // 途中の角のうち、NavMesh の縁に当たらず直接行ける所までは、角を飛ばして近道にする。
        private static List<Vector3> Shortcut(Vector3[] corners)
        {
            var result = new List<Vector3> { corners[0] };
            var from = 0;
            while (from < corners.Length - 1)
            {
                var next = from + 1;
                for (var j = corners.Length - 1; j > from + 1; j--)
                {
                    if (WalksStraight(corners[from], corners[j]))
                    {
                        next = j;
                        break;
                    }
                }
                result.Add(corners[next]);
                from = next;
            }
            return result;
        }

        // 曲がり角だけを丸め、真っすぐな区間は真っすぐのままにする(通路の外へ膨らませない)。
        // 丸めた線が壁や物にかかる角は、丸める大きさを小さくして避ける(最後は丸めずに角のまま)。
        // そのあと一定間隔に並べ直して、床の高さに合わせる。
        private void BuildGoal(Vector3[] rawCorners)
        {
            var corners = Shortcut(rawCorners);
            _dense.Clear();
            _dense.Add(corners[0]);
            for (var i = 1; i < corners.Count - 1; i++)
            {
                var before = corners[i - 1];
                var corner = corners[i];
                var after = corners[i + 1];
                // 丸める大きさは、前後の区間の半分までに収める(隣の角と重ならないように)。
                var radius = Mathf.Min(
                    BendRadius,
                    Vector3.Distance(before, corner) * 0.5f,
                    Vector3.Distance(corner, after) * 0.5f
                );
                for (var attempt = 0; attempt < BendAttempts && radius > 0.05f; attempt++)
                {
                    if (TryRound(before, corner, after, radius))
                        break;
                    radius *= 0.5f;
                    if (attempt == BendAttempts - 1)
                        radius = 0f;
                }
                if (radius <= 0.05f)
                    _dense.Add(corner); // 丸めずに角のまま
            }
            _dense.Add(corners[corners.Count - 1]);

            _goal.Clear();
            _goal.Add(OnFloor(_dense[0]));
            var carried = 0f; // 直前の点から進んだ距離
            for (var i = 1; i < _dense.Count; i++)
            {
                var a = _dense[i - 1];
                var b = _dense[i];
                var segment = Vector3.Distance(a, b);
                var at = PointSpacing - carried;
                while (at <= segment)
                {
                    _goal.Add(OnFloor(Vector3.Lerp(a, b, at / segment)));
                    at += PointSpacing;
                }
                carried = segment - (at - PointSpacing);
            }
            _goal.Add(OnFloor(_dense[_dense.Count - 1]));

            RaiseOverStairEdges();
        }

        // 階段では、点の間の線が段の角(縁)の下をくぐって見える。隣の点の高さと比べて高いほうに揃え、
        // 線が段の角にめり込まないようにする。平らな床では高さが変わらないので影響しない。
        private void RaiseOverStairEdges()
        {
            for (var pass = 0; pass < StairSmoothPasses; pass++)
            {
                var heights = new float[_goal.Count];
                for (var i = 0; i < _goal.Count; i++)
                {
                    var y = _goal[i].y;
                    if (i > 0)
                        y = Mathf.Max(y, _goal[i - 1].y);
                    if (i < _goal.Count - 1)
                        y = Mathf.Max(y, _goal[i + 1].y);
                    heights[i] = y;
                }
                for (var i = 0; i < _goal.Count; i++)
                {
                    var p = _goal[i];
                    p.y = heights[i];
                    _goal[i] = p;
                }
            }
        }

        // 角を丸めた点が、NavMesh の外へ出たり物にぶつかったりしなければ _dense に足して true を返す。
        private bool TryRound(Vector3 before, Vector3 corner, Vector3 after, float radius)
        {
            var entry = corner + (before - corner).normalized * radius;
            var exit = corner + (after - corner).normalized * radius;
            var points = new Vector3[BendSteps + 1];
            for (var s = 0; s <= BendSteps; s++)
            {
                var t = s / (float)BendSteps;
                points[s] = Vector3.Lerp(
                    Vector3.Lerp(entry, corner, t),
                    Vector3.Lerp(corner, exit, t),
                    t
                );
            }

            for (var s = 1; s < points.Length; s++)
                if (Blocked(points[s - 1], points[s]))
                    return false;

            _dense.AddRange(points);
            return true;
        }

        // 2点の間が、NavMesh から外れる(壁・縁・別の階をまたぐ)、または物にぶつかるなら true。
        private bool Blocked(Vector3 a, Vector3 b)
        {
            if (!WalksStraight(a, b))
                return true;

            // NavMesh に載らない小物は、当たり判定で調べる(床と同じ高さだと床に当たるので少し上げる)。
            var lift = Vector3.up * ObstacleCheckHeight;
            return Physics.Linecast(
                    a + lift,
                    b + lift,
                    out var hit,
                    ~0,
                    QueryTriggerInteraction.Ignore
                ) && !hit.collider.CompareTag("Player");
        }

        // 2点を結ぶ直線が、ずっと同じ NavMesh の面の上(高さも含めて)にあるか。
        // NavMesh.Raycast は縁に当たらないかだけを見るので、階をまたぐ直線(床や天井を貫く線)を通してしまう。
        // そこで、途中の点がどれも NavMesh の近くにあることも確かめる。
        private static bool WalksStraight(Vector3 a, Vector3 b)
        {
            if (NavMesh.Raycast(a, b, out _, NavMesh.AllAreas))
                return false;

            var length = Vector3.Distance(a, b);
            var steps = Mathf.Clamp(Mathf.CeilToInt(length / 0.5f), 1, MaxStraightSamples);
            for (var i = 1; i < steps; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)steps);
                if (!NavMesh.SamplePosition(p, out _, OnPathTolerance, NavMesh.AllAreas))
                    return false;
            }
            return true;
        }

        // 線の高さは物理の床ではなく NavMesh の面から取る。階が重なる建物でも、自分の階の面だけを拾える
        // (上や下の階の床へ吸い付かない)。見た目の床との差は LiftAboveFloor で埋める。
        private static Vector3 OnFloor(Vector3 point)
        {
            point.y = TryFloorY(point, out var floorY) ? floorY : point.y;
            point.y += LiftAboveFloor;
            return point;
        }

        // 点の近くの NavMesh の高さ。近くに面が無ければ false。
        private static bool TryFloorY(Vector3 point, out float y)
        {
            y = point.y;
            if (!NavMesh.SamplePosition(point, out var hit, VerticalTolerance, NavMesh.AllAreas))
                return false;
            y = hit.position.y;
            return true;
        }

        // 描いている形を、最新の道筋へ少しずつ近づける(0.4 秒ごとに形が飛ばないように)。
        private void FollowGoal()
        {
            if (_goal.Count < 2)
                return;

            // 点の数が変わったときは、足りない分を目標の形で補い、余った分は切る。
            while (_shown.Count < _goal.Count)
                _shown.Add(_goal[_shown.Count]);
            if (_shown.Count > _goal.Count)
                _shown.RemoveRange(_goal.Count, _shown.Count - _goal.Count);

            var k = 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime);
            for (var i = 0; i < _shown.Count; i++)
                _shown[i] = Vector3.Lerp(_shown[i], _goal[i], k);

            // プレイヤーの近くは、歩いて床の高さが変わる(階段など)ので、毎フレーム床に合わせ直す。
            // 経路の計算は 0.4 秒おきなので、これをしないと階段で線が床にめり込む。
            if (_player != null)
            {
                var feet = _player.position;
                // 出だしはプレイヤーの足元に合わせる(歩いてもすぐ追いつく)。
                var head = _shown[0];
                head.x = feet.x;
                head.z = feet.z;
                _shown[0] = head;
                for (var i = 0; i < _shown.Count; i++)
                {
                    var p = _shown[i];
                    if ((p - feet).sqrMagnitude > NearSnapRadius * NearSnapRadius)
                        break; // 道筋は手前から順に並ぶので、遠くに出たらそこまで
                    if (TryFloorY(p, out var y))
                    {
                        p.y = y + LiftAboveFloor;
                        _shown[i] = p;
                    }
                }
            }

            _line.positionCount = _shown.Count;
            for (var i = 0; i < _shown.Count; i++)
                _line.SetPosition(i, _shown[i]);
        }

        // 矢じりをピンの向きへ流す(画像の模様をずらすだけ)。
        private void Flow()
        {
            var offset = -(Time.time * FlowSpeed / ArrowSpacing) % 1f;
            _block.SetVector(MainTexSt, new Vector4(1f, 1f, offset, 0f));
            _line.SetPropertyBlock(_block);
        }

        public void Destroy()
        {
            if (_line == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(_line.gameObject);
            else
                Object.DestroyImmediate(_line.gameObject);
        }
    }
}
