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
        private const string MaterialResource = "MapPinGroundLine";
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
        private const float MinBendRadius = 0.05f; // これより小さい丸めは意味がないので角のままにする
        private const float ObstacleCheckHeight = 0.5f; // 物との当たりを調べる、床からの高さ
        private const float FollowSharpness = 14f; // 新しい形へ近づく速さ(大きいほど速い)
        private const float NearSnapRadius = 5f; // この近さの点は毎フレーム床に合わせ直す
        private const float VerticalTolerance = 1f; // 線の高さを取る NavMesh を探す範囲(階の高さより十分小さく)
        private const float OnPathTolerance = 0.35f; // 直線の途中の点が NavMesh からずれてよい範囲
        private const int MaxStraightSamples = 20; // 直線の確認で調べる点の数の上限(重くしないため)
        private const float FadeSeconds = 0.25f; // 出入りの透明度が変わりきるまで
        private const float MaxVerticalReach = 4f; // プレイヤーの足元からこれ以上高さが違う所で線を打ち切る(上下の階まで抜けて見えないように)
        private const float NearFadeLength = 2.5f; // 線の出だしが薄い長さ(ワールド長。道筋の長さによらない)
        private const float FarFadeLength = 4f; // 線の終わりが薄れていく長さ
        private const float EdgeClearance = 2f; // 広い所で線を NavMesh の縁(壁・手すり)から離す距離。幅がこの2倍以下の所は真ん中に合わせる
        private const float EdgeSearchWidth = 8f; // 反対側の縁までの幅を測る範囲(これより広ければ「広い所」)
        private const float EdgeClearanceRange = 30f; // 縁から離す処理をかける、プレイヤーからの道のり
        private const float JoinLookaheadPerOffset = 4f; // 道筋から横にずれた分の何倍先へ斜めにつなぐか
        private const float MaxJoinLookahead = 2f; // 斜めにつなぐ先の上限(道筋に沿った距離)
        private const int NearSearchPoints = 60; // 足元に一番近い線分を探す範囲(手前から数えた点の数)
        private static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly LineRenderer _line;
        private readonly NavMeshPath _path = new();
        private readonly MaterialPropertyBlock _block = new();
        private readonly List<Vector3> _goal = new(); // 計算で求めた最新の道筋(目標の形)
        private readonly List<Vector3> _shown = new(); // 実際に描いている道筋(目標へ近づけていく)
        private readonly List<Vector3> _dense = new();
        private readonly List<Vector3> _corners = new(); // 近道にした後の角(使い回す)
        private readonly List<float> _heights = new(); // 階段の持ち上げ用(使い回す)
        private readonly Vector3[] _bend = new Vector3[BendSteps + 1]; // 丸めた角の点(使い回す)
        private readonly List<bool> _moved = new(); // 縁から寄せて動いた点(横ずれをならす範囲を絞る。使い回す)
        private readonly List<Vector3> _smoothed = new(); // 横ずれをならす作業用(使い回す)
        private readonly List<Vector3> _display = new(); // 実際に線へ渡す点(足元から先だけ)
        private float _displayLength; // _display の長さ(矢じりの模様の位置と薄れの計算に使う)
        private readonly Gradient _fade = new();
        private readonly GradientColorKey[] _fadeColors =
        {
            new(Color.white, 0f),
            new(Color.white, 1f),
        };
        private readonly GradientAlphaKey[] _fadeAlphas = new GradientAlphaKey[4];
        private Color _baseColor = Color.white; // 材質の色(透明度だけを掛けて変える)
        private float _nextRefresh;
        private bool _visible; // 見せたい状態。false なら透明になってから消す
        private float _alpha; // 線の透明度の倍率(0 で見えない)

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
            UpdateFade(Mathf.Infinity);
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.allowOcclusionWhenDynamic = false;
            var material = Resources.Load<Material>(MaterialResource);
            if (material != null)
            {
                _line.sharedMaterial = material;
                _baseColor = material.GetColor(ColorId);
            }
            else
                Debug.LogWarning($"[MapPin] Resources/{MaterialResource} が見つかりません。");
            _line.enabled = false;
        }

        /// <summary>wanted が false の間は線を隠す(計算もしない)。</summary>
        public void Tick(Vector3 target, bool wanted)
        {
            if (_line == null)
                return;
            if (!wanted)
                _visible = false;
            else if (Time.time >= _nextRefresh)
            {
                _nextRefresh = Time.time + RefreshSeconds;
                Refresh(target);
            }
            if (!_line.enabled)
                return;

            // 消すときも、すぐには切らずに透明にしてから消す。
            _alpha = Mathf.MoveTowards(_alpha, _visible ? 1f : 0f, Time.deltaTime / FadeSeconds);
            if (!_visible && _alpha <= 0f)
            {
                Hide();
                return;
            }
            FollowGoal();
            Flow();
        }

        // 手前と奥を薄くして、線の出だしと終わりが急に切れて見えないようにする。
        // 薄れる長さは道筋の長さによらず一定にする(割合だと長い道で手元の矢じりが長く薄いままになる)。
        private void UpdateFade(float length)
        {
            var inEnd =
                length > 0f && !float.IsInfinity(length)
                    ? Mathf.Min(NearFadeLength, length * 0.4f) / length
                    : 0.12f;
            var outStart =
                length > 0f && !float.IsInfinity(length)
                    ? 1f - Mathf.Min(FarFadeLength, length * 0.4f) / length
                    : 0.8f;
            _fadeAlphas[0] = new GradientAlphaKey(0f, 0f);
            _fadeAlphas[1] = new GradientAlphaKey(1f, inEnd);
            _fadeAlphas[2] = new GradientAlphaKey(1f, outStart);
            _fadeAlphas[3] = new GradientAlphaKey(0f, 1f);
            _fade.SetKeys(_fadeColors, _fadeAlphas);
            _line.colorGradient = _fade;
        }

        private void Refresh(Vector3 target)
        {
            var player = MapPin.PlayerTransform;
            if (player == null)
            {
                Hide();
                return;
            }

            var from = player.position;
            if (Vector3.Distance(from, target) < HideWithin)
            {
                _visible = false;
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
                _visible = false;
                return;
            }

            BuildGoal(_path.corners);
            _visible = true;
            if (!_line.enabled)
            {
                // 出始めは目標の形をそのまま使う(遠くから縮んでくるのを避ける)。
                _shown.Clear();
                _shown.AddRange(_goal);
                _alpha = 0f;
                _line.enabled = true;
            }
        }

        private void Hide()
        {
            _alpha = 0f;
            _line.enabled = false;
            _goal.Clear();
            _shown.Clear();
        }

        // NavMesh の経路は「最短」ではなく「コストが最小」の道なので、真っすぐ歩ける所でも遠回りすることがある。
        // 途中の角のうち、NavMesh の縁に当たらず直接行ける所までは、角を飛ばして近道にする。
        private List<Vector3> Shortcut(Vector3[] corners)
        {
            var result = _corners;
            result.Clear();
            result.Add(corners[0]);
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
                // 通れなければ大きさを半分にして試し、どれもだめなら丸めずに角のままにする。
                var rounded = false;
                for (var attempt = 0; attempt < BendAttempts && radius > MinBendRadius; attempt++)
                {
                    rounded = TryRound(before, corner, after, radius);
                    if (rounded)
                        break;
                    radius *= 0.5f;
                }
                if (!rounded)
                    _dense.Add(corner);
            }
            _dense.Add(corners[corners.Count - 1]);

            // 点は終点から数えて一定間隔に並べる。プレイヤーが歩いて出だしが動いても、終点側の点の
            // ワールド位置が変わらないので、計算し直したときに線がずれたり、矢じりが前後に揺れたりしない。
            _goal.Clear();
            _goal.Add(OnFloor(_dense[_dense.Count - 1]));
            var carried = 0f; // 直前の点から進んだ距離
            for (var i = _dense.Count - 1; i > 0; i--)
            {
                var a = _dense[i];
                var b = _dense[i - 1];
                var segment = Vector3.Distance(a, b);
                var at = PointSpacing - carried;
                while (at <= segment)
                {
                    _goal.Add(OnFloor(Vector3.Lerp(a, b, at / segment)));
                    at += PointSpacing;
                }
                carried = segment - (at - PointSpacing);
            }
            _goal.Add(OnFloor(_dense[0]));
            _goal.Reverse(); // 並びはプレイヤー側から終点へ

            KeepOffEdges();
            RaiseOverStairEdges();
        }

        // NavMesh の経路は角を最短で通るので、階段や通路の縁(壁・手すり)ぎりぎりにはり付く。
        // 線はざっくりした案内なので、少し大回りになってもよい。縁から離して、歩ける幅の内側へ寄せる。
        //   幅が狭い所(ドア・1マスの通路・階段) … 左右の縁の真ん中に合わせる
        //   広い所 … 縁から EdgeClearance まで離す
        // 手すりのように NavMesh に反映されない物でも、縁から離れていれば重ならない。
        // 遠くは見えにくく計算も重いので EdgeClearanceRange までを対象にし、その手前から少しずつ効きを弱める。
        // 高さは寄せた先の NavMesh の面から取り直す(階をまたがない)。
        private void KeepOffEdges()
        {
            var travelled = 0f;
            var last = 0;
            _moved.Clear();
            for (var i = 0; i < _goal.Count; i++)
            {
                if (i > 0)
                    travelled += Vector3.Distance(_goal[i - 1], _goal[i]);
                if (travelled > EdgeClearanceRange)
                    break;
                last = i;
                var strength =
                    1f
                    - Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(EdgeClearanceRange * 0.75f, EdgeClearanceRange, travelled)
                    );
                var centered = CenterWithinWalkable(_goal[i], strength);
                _moved.Add((centered - _goal[i]).sqrMagnitude > 0.0001f);
                _goal[i] = centered;
            }
            SmoothSideways(last);
        }

        // 点を、歩ける幅の内側へ寄せた位置を返す。寄せられない・寄せる必要がなければそのまま返す。
        private static Vector3 CenterWithinWalkable(Vector3 point, float strength)
        {
            var onFloor = point - Vector3.up * LiftAboveFloor;
            // 一番近い縁の向きを「幅を測る軸」にする(縁のどちら側かは次の測定で決める)。
            if (!NavMesh.FindClosestEdge(onFloor, out var edge, NavMesh.AllAreas))
                return point;
            // 縁から EdgeClearance 以上離れた点は広い所なので動かさない(幅を測る問い合わせを省く)。
            if (edge.distance >= EdgeClearance)
                return point;
            var axis = new Vector3(edge.normal.x, 0f, edge.normal.z);
            if (axis.sqrMagnitude < 0.0001f)
                return point;
            axis.Normalize();

            var plus = WalkableDistance(onFloor, axis);
            var minus = WalkableDistance(onFloor, -axis);
            var near = Mathf.Min(plus, minus);
            var far = Mathf.Max(plus, minus);
            var towardFar = plus >= minus ? axis : -axis;

            // 反対側の縁が測れた(= 幅が狭い)なら真ん中へ。測れない(広い)なら縁から EdgeClearance まで。
            var narrow = far < EdgeSearchWidth && near + far <= 2f * EdgeClearance;
            var shift = narrow ? (far - near) * 0.5f : Mathf.Max(0f, EdgeClearance - near);
            shift *= strength;
            if (shift <= 0.01f)
                return point;

            var target = onFloor + towardFar * shift;
            if (
                !NavMesh.SamplePosition(target, out var onMesh, VerticalTolerance, NavMesh.AllAreas)
            )
                return point;
            return onMesh.position + Vector3.up * LiftAboveFloor;
        }

        // direction へ NavMesh の縁にぶつかるまでの距離。EdgeSearchWidth まで縁が無ければ EdgeSearchWidth。
        private static float WalkableDistance(Vector3 from, Vector3 direction) =>
            NavMesh.Raycast(from, from + direction * EdgeSearchWidth, out var hit, NavMesh.AllAreas)
                ? hit.distance
                : EdgeSearchWidth;

        // 寄せたあとの横ずれの段差をならす(隣の点と少し混ぜる)。NavMesh から外れる所は動かさない。
        private void SmoothSideways(int last)
        {
            if (last < 2)
                return;
            _smoothed.Clear();
            _smoothed.AddRange(_goal);
            for (var i = 1; i < last; i++)
            {
                // 動いていない点の周りは、ならす必要がない(問い合わせを省く)。
                if (!_moved[i - 1] && !_moved[i] && !_moved[i + 1])
                    continue;
                var p = _goal[i];
                var blended = (_goal[i - 1] + 2f * p + _goal[i + 1]) * 0.25f;
                blended.y = p.y; // 高さは混ぜない(階段で線が面からずれないように)
                var onFloor = blended - Vector3.up * LiftAboveFloor;
                if (
                    NavMesh.SamplePosition(
                        onFloor,
                        out var onMesh,
                        OnPathTolerance,
                        NavMesh.AllAreas
                    )
                )
                    _smoothed[i] = onMesh.position + Vector3.up * LiftAboveFloor;
            }
            for (var i = 1; i < last; i++)
                _goal[i] = _smoothed[i];
        }

        // 階段などで、プレイヤーの足元から高さが大きく離れた所から先は描かない。
        // 床や天井は片面だけの面のことがあり、下の階から上の階の線が抜けて見えてしまうため。
        // 上り始めは段の分だけ先まで見え、上るにつれて先が伸びていく。
        // 点の並びを変えるので、_goal ではなく描く直前の _display に対して毎フレーム行う
        // (_goal は終点側をそろえて _shown と対応づけるので、終点側を削ると対応がずれる)。
        private void TrimDisplayToReach(float baseY)
        {
            for (var i = 2; i < _display.Count; i++)
            {
                if (Mathf.Abs(_display[i].y - baseY) <= MaxVerticalReach)
                    continue;
                _display.RemoveRange(i, _display.Count - i);
                break;
            }
        }

        private static float PathLength(List<Vector3> points)
        {
            var length = 0f;
            for (var i = 1; i < points.Count; i++)
                length += Vector3.Distance(points[i - 1], points[i]);
            return length;
        }

        // 階段では、点の間の線が段の角(縁)の下をくぐって見える。隣の点の高さと比べて高いほうに揃え、
        // 線が段の角にめり込まないようにする。平らな床では高さが変わらないので影響しない。
        private void RaiseOverStairEdges()
        {
            for (var pass = 0; pass < StairSmoothPasses; pass++)
            {
                _heights.Clear();
                for (var i = 0; i < _goal.Count; i++)
                {
                    var y = _goal[i].y;
                    if (i > 0)
                        y = Mathf.Max(y, _goal[i - 1].y);
                    if (i < _goal.Count - 1)
                        y = Mathf.Max(y, _goal[i + 1].y);
                    _heights.Add(y);
                }
                for (var i = 0; i < _goal.Count; i++)
                {
                    var p = _goal[i];
                    p.y = _heights[i];
                    _goal[i] = p;
                }
            }
        }

        // 角を丸めた点が、NavMesh の外へ出たり物にぶつかったりしなければ _dense に足して true を返す。
        private bool TryRound(Vector3 before, Vector3 corner, Vector3 after, float radius)
        {
            var entry = corner + (before - corner).normalized * radius;
            var exit = corner + (after - corner).normalized * radius;
            var points = _bend;
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

        // 描いている形を、最新の道筋へ少しずつ近づける(経路を計算し直すたびに形が飛ばないように)。
        private void FollowGoal()
        {
            if (_goal.Count < 2)
                return;

            // 点の数が変わったときは、終点側をそろえて、増減した分を手前(プレイヤー側)で補う・切る。
            // 手前の点は歩くたびに増減するので、終点側をそろえないと形全体が前後にずれる。
            var extra = _goal.Count - _shown.Count;
            for (var i = extra - 1; i >= 0; i--)
                _shown.Insert(0, _goal[i]);
            if (extra < 0)
                _shown.RemoveRange(0, -extra);

            var k = 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime);
            for (var i = 0; i < _shown.Count; i++)
                _shown[i] = Vector3.Lerp(_shown[i], _goal[i], k);

            // プレイヤーの近くは、歩いて床の高さが変わる(階段など)ので、毎フレーム床に合わせ直す。
            // 経路の計算は一定間隔なので、これをしないと階段で線が床にめり込む。
            var player = MapPin.PlayerTransform;
            var feet = player != null ? player.position : _shown[0];
            if (player != null)
            {
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

            BuildDisplay(feet);
            _line.positionCount = _display.Count;
            for (var i = 0; i < _display.Count; i++)
                _line.SetPosition(i, _display[i]);
            UpdateFade(_displayLength);
        }

        // 描く線は「道筋の上の、プレイヤーの足元に一番近い点」から先だけにする。
        // 経路は一定間隔でしか計算し直さないので、歩いて追い越した分の古い出だしが残る。
        // それを足元からつなぐと、足元から古い出だしへ戻ってから進む「Uターン」に見えてしまう。
        private void BuildDisplay(Vector3 feet)
        {
            _display.Clear();
            var bestSegment = 0;
            var bestPoint = _shown[0];
            var bestSqr = float.MaxValue;
            var searchEnd = Mathf.Min(_shown.Count - 1, NearSearchPoints);
            for (var i = 0; i < searchEnd; i++)
            {
                var point = ClosestOnSegment(_shown[i], _shown[i + 1], feet);
                var sqr = (point - feet).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestSegment = i;
                    bestPoint = point;
                }
            }

            // 根元は足元に付ける(左右に動いても線がついてくる)。道筋から横にずれているほど、道筋の少し先へ
            // 斜めにつなぎ、真後ろの古い点には戻らない。道筋の上にいるときは、そのまま道筋をたどる。
            var offset = Vector2.Distance(
                new Vector2(feet.x, feet.z),
                new Vector2(bestPoint.x, bestPoint.z)
            );
            var lookahead = Mathf.Min(offset * JoinLookaheadPerOffset, MaxJoinLookahead);
            _display.Add(new Vector3(feet.x, bestPoint.y, feet.z));
            var next = bestSegment + 1;
            var travelled = 0f;
            var previous = bestPoint;
            while (next < _shown.Count - 1)
            {
                var step = Vector3.Distance(previous, _shown[next]);
                if (travelled + step > lookahead)
                    break;
                travelled += step;
                previous = _shown[next];
                next++;
            }
            for (var i = next; i < _shown.Count; i++)
                _display.Add(_shown[i]);
            if (_display.Count < 2)
                _display.Add(_shown[_shown.Count - 1]);

            TrimDisplayToReach(feet.y);
            _displayLength = PathLength(_display);
        }

        private static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            var ab = b - a;
            var lengthSqr = ab.sqrMagnitude;
            if (lengthSqr <= 0f)
                return a;
            var t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSqr);
            return a + ab * t;
        }

        // 矢じりを終点へ向けて流す(画像の模様をずらすだけ)。
        // 模様は終点から数えた位置に固定し、そこへ流れる分だけ足す。出だし(足元)を基準にすると、
        // 歩いて足元が動くたびに矢じり全体が一緒にずれて、カクカク進んで見える。
        private void Flow()
        {
            var offset = Mathf.Repeat(-(_displayLength + Time.time * FlowSpeed) / ArrowSpacing, 1f);
            _block.SetVector(MainTexSt, new Vector4(1f, 1f, offset, 0f));
            var color = _baseColor;
            color.a *= _alpha;
            _block.SetColor(ColorId, color);
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
