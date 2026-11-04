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
        private const float RefreshSeconds = 0.4f;
        private const float HideWithin = 2.5f; // この近さまで来たら消す
        private const float SnapDistance = 3f; // ピンの真下の NavMesh を探す範囲
        private const float LiftAboveFloor = 0.08f; // 床に埋まらないよう少し浮かせる
        private const float Width = 0.9f; // 矢じり1つの幅
        private const float ArrowSpacing = 1.4f; // 矢じりの間隔(ワールド長)
        private const float FlowSpeed = 0.9f; // 矢じりが進む速さ(ワールド長/秒)
        private const float PointSpacing = 0.5f; // 線の点の間隔(曲線のなめらかさ)
        private const float BendRadius = 4f; // 曲がり角を丸める大きさ
        private const int BendSteps = 10; // 丸めた角の細かさ
        private const float FollowSharpness = 14f; // 新しい形へ近づく速さ(大きいほど速い)
        private const float FloorRayUp = 0.6f;
        private const float FloorRayLength = 1.6f;
        private const float FloorMaxDelta = 0.5f; // NavMesh の高さとこれ以上ずれた床は無視する
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
                    if (!NavMesh.Raycast(corners[from], corners[j], out _, NavMesh.AllAreas))
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

        // 曲がり角だけを小さく丸め、真っすぐな区間は真っすぐのままにする(通路の外へ膨らませない)。
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
                var entry = corner + (before - corner).normalized * radius;
                var exit = corner + (after - corner).normalized * radius;
                for (var s = 0; s <= BendSteps; s++)
                {
                    var t = s / (float)BendSteps;
                    _dense.Add(
                        Vector3.Lerp(
                            Vector3.Lerp(entry, corner, t),
                            Vector3.Lerp(corner, exit, t),
                            t
                        )
                    );
                }
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
        }

        // NavMesh の面は見た目の床と少しずれるので、真下の床に合わせる。
        // 小物の上などに乗らないよう、NavMesh の高さから離れすぎた面は無視する。
        private static Vector3 OnFloor(Vector3 point)
        {
            var origin = point + Vector3.up * FloorRayUp;
            if (
                Physics.Raycast(
                    origin,
                    Vector3.down,
                    out var hit,
                    FloorRayLength,
                    ~0,
                    QueryTriggerInteraction.Ignore
                )
                && hit.normal.y > 0.7f
                && Mathf.Abs(hit.point.y - point.y) <= FloorMaxDelta
            )
                point.y = hit.point.y;
            point.y += LiftAboveFloor;
            return point;
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

            // 出だしはプレイヤーの足元に毎フレーム合わせる(歩いてもすぐ追いつく)。
            if (_player != null)
            {
                var head = _shown[0];
                head.x = _player.position.x;
                head.z = _player.position.z;
                _shown[0] = head;
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
