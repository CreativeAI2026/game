using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Core
{
    /// <summary>
    /// プレイヤーからマップピンまでの道筋を、地面に沿う線で描く(NavMesh の経路)。
    /// MapPin が持ち、Tick を毎フレーム呼ぶ。経路の計算は一定間隔でだけ行う。
    /// 経路が無い(NavMesh が途切れている、別の階など)・近づいた・プレイヤーが居ないときは線を消す。
    /// </summary>
    public sealed class MapPinPathLine
    {
        public const string MaterialResource = "MapPinGroundLine";
        private const float RefreshSeconds = 0.4f;
        private const float HideWithin = 2.5f; // この近さまで来たら消す
        private const float SnapDistance = 3f; // ピンの真下の NavMesh を探す範囲
        private const float LiftAboveFloor = 0.08f; // 床に埋まらないよう少し浮かせる
        private const float Width = 0.3f;

        private readonly LineRenderer _line;
        private readonly NavMeshPath _path = new();
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
            _line.numCornerVertices = 4;
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
            if (_line == null || Time.time < _nextRefresh)
                return;
            _nextRefresh = Time.time + RefreshSeconds;
            Refresh(target);
        }

        private void Refresh(Vector3 target)
        {
            var player = FindPlayer();
            if (player == null)
            {
                _line.enabled = false;
                return;
            }

            var from = player.position;
            if (Vector3.Distance(from, target) < HideWithin)
            {
                _line.enabled = false;
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
                _line.enabled = false;
                return;
            }

            var corners = _path.corners;
            _line.positionCount = corners.Length;
            for (var i = 0; i < corners.Length; i++)
                _line.SetPosition(i, corners[i] + Vector3.up * LiftAboveFloor);
            _line.enabled = true;
        }

        private static Transform FindPlayer()
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            return go != null ? go.transform : null;
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
