using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 輪(トーラス)のメッシュの半径だけを変える。輪の太さ(管の太さ)は変えない。
    /// 拡大縮小では太さまで細くなってしまうため、頂点を輪の中心から外向きの方向にだけずらす。
    ///
    /// 輪の中心と向きはメッシュの範囲から求める(一番薄い軸を輪の軸とみなす)。
    /// 元のメッシュアセットは書き換えず、複製したメッシュを変形する。頂点を読むため、モデルの Read/Write を有効にしておくこと。
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class RingRadiusDeformer : MonoBehaviour
    {
        private Mesh _mesh;
        private Vector3[] _originalVertices;
        private Vector3[] _deformedVertices;
        private Vector3 _center;
        private Vector3 _axis;
        private float _originalRadiusLocal;
        private float _currentRadiusLocal;

        /// <summary>元のメッシュの輪の半径(管の中心までの半径)。ワールドの長さ（m）。</summary>
        public float OriginalRadius => _originalRadiusLocal * LocalToWorldScale;

        /// <summary>今の輪の半径。ワールドの長さ（m）。</summary>
        public float CurrentRadius => _currentRadiusLocal * LocalToWorldScale;

        /// <summary>輪の中心(ワールド座標)。</summary>
        public Vector3 WorldCenter => transform.TransformPoint(_center);

        /// <summary>輪の軸(ワールドの向き)。</summary>
        public Vector3 WorldAxis => transform.TransformDirection(_axis).normalized;

        /// <summary>輪の管の半径(太さの半分)。ワールドの長さ（m）。</summary>
        public float TubeRadius { get; private set; }

        // 輪は均等に拡大縮小されている前提。輪の面の向きのスケールを使う
        private float LocalToWorldScale => transform.lossyScale.x;

        private void Awake()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            Assert.IsNotNull(meshFilter.sharedMesh, $"{name}: メッシュがありません");
            Assert.IsTrue(
                meshFilter.sharedMesh.isReadable,
                $"{name}: メッシュの頂点を読めません。モデルのインポート設定で Read/Write を有効にしてください"
            );

            _mesh = Instantiate(meshFilter.sharedMesh);
            _mesh.MarkDynamic();
            meshFilter.mesh = _mesh;

            _originalVertices = _mesh.vertices;
            _deformedVertices = new Vector3[_originalVertices.Length];

            Bounds bounds = _mesh.bounds;
            _center = bounds.center;
            Vector3 extents = bounds.extents;
            int thinAxis =
                extents.x <= extents.y && extents.x <= extents.z ? 0
                : extents.y <= extents.z ? 1
                : 2;
            _axis = Vector3.zero;
            _axis[thinAxis] = 1f;

            // 管の半径 = 軸方向の厚みの半分。輪の半径 = 外周の半径 - 管の半径
            float tubeRadiusLocal = extents[thinAxis];
            float outerRadiusLocal = extents[(thinAxis + 1) % 3];
            _originalRadiusLocal = outerRadiusLocal - tubeRadiusLocal;
            _currentRadiusLocal = _originalRadiusLocal;
            TubeRadius = tubeRadiusLocal * LocalToWorldScale;
        }

        /// <summary>輪の半径をワールドの長さ（m）で指定する。管の太さより小さくはならない。</summary>
        public void SetRadius(float worldRadius)
        {
            float radiusLocal = Mathf.Max(worldRadius / LocalToWorldScale, 0f);
            if (Mathf.Approximately(radiusLocal, _currentRadiusLocal))
                return;

            _currentRadiusLocal = radiusLocal;
            float shrink = _originalRadiusLocal - radiusLocal;

            for (int i = 0; i < _originalVertices.Length; i++)
            {
                Vector3 offset = _originalVertices[i] - _center;
                Vector3 radial = offset - _axis * Vector3.Dot(offset, _axis);
                float length = radial.magnitude;
                _deformedVertices[i] =
                    length > 1e-6f
                        ? _originalVertices[i] - radial / length * shrink
                        : _originalVertices[i];
            }

            _mesh.vertices = _deformedVertices;
            _mesh.RecalculateBounds();
        }

        /// <summary>元の半径に戻す。</summary>
        public void ResetRadius() => SetRadius(OriginalRadius);

        private void OnDestroy()
        {
            if (_mesh != null)
                Destroy(_mesh);
        }
    }
}
