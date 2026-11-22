using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// ミニマップの1つの階を、距離フィールドのテクスチャ(<see cref="MinimapLayout.Level.Field"/>)で描く。
    /// 四角形を、階の図に 1 枚と階段ごとに 1 枚だけ作り、縁のなめらかさは専用のシェーダー(MinimapShape)が決める。
    /// 頂点はワールドの XZ そのまま(1 = 1u)で、図の左下を原点にした相対位置。表示の大きさは親の scale で決まる。
    /// <see cref="MinimapMap"/> が階の数だけ作って使う。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MinimapLevelGraphic : MaskableGraphic
    {
        // 階段の四角形を、端のなめらかな縁を描くために外へ広げる量(u)
        private const float StairMargin = 1f;

        private static readonly int IdTexId = Shader.PropertyToID("_IdTex");

        private MinimapLayout.Level _level;
        private float _stepSpacing = 0.9f;
        private Material _instance; // 階ごとに ID 画像が違うので、階ごとに持つ

        /// <summary>
        /// 描く階を決める。<paramref name="template"/> のシェーダーの設定を複製して、この階のテクスチャを付ける。
        /// </summary>
        public void Show(MinimapLayout.Level level, Material template, float stepSpacing)
        {
            _level = level;
            _stepSpacing = stepSpacing;
            ApplyMaterial(template);
            EnableShaderChannels();

            var rect = level.FieldRect;
            rectTransform.sizeDelta = rect.size;
            SetAllDirty();
        }

        /// <summary>
        /// 色や太さを変えた <paramref name="template"/> を、描いている階にそのまま当て直す(Play 中に Inspector で変えたとき)。
        /// </summary>
        public void Refresh(Material template, float stepSpacing)
        {
            if (_level == null)
                return;
            _stepSpacing = stepSpacing;
            ApplyMaterial(template);
            SetVerticesDirty();
        }

        // マテリアルは毎回作り直す。Mask の中では Unity がマテリアルの複製をキャッシュするので、
        // 同じマテリアルのテクスチャや値を書き換えても、描画に使われる複製には届かない
        private void ApplyMaterial(Material template)
        {
            DestroyInstance();
            _instance = new Material(template) { hideFlags = HideFlags.HideAndDontSave };
            _instance.SetTexture(IdTexId, _level.Ids);
            material = _instance;
        }

        private void DestroyInstance()
        {
            if (_instance == null)
                return;
            if (Application.isPlaying)
                Destroy(_instance);
            else
                DestroyImmediate(_instance);
            _instance = null;
        }

        /// <summary>
        /// 階の図の位置を置く。親の pivot を、ワールドの原点に当たる点として、そこからの位置で決める。
        /// </summary>
        public void Place(Vector2 originPivot)
        {
            rectTransform.anchorMin = originPivot;
            rectTransform.anchorMax = originPivot;
            rectTransform.pivot = Vector2.zero;
            rectTransform.anchoredPosition = _level.FieldRect.min;
        }

        /// <summary>テクスチャの中の距離を、画面のピクセルへなめらかに写す。</summary>
        public override Texture mainTexture => _level != null ? _level.Field : null;

        protected override void OnEnable()
        {
            base.OnEnable();
            EnableShaderChannels();
        }

        // シェーダーへ図の大きさと階段の段の情報を渡す uv1 を、キャンバスが送るようにする。
        // MinimapMap が作った直後はまだ親が無く canvas が null なので、OnEnable だけでなく Show でも呼ぶ
        private void EnableShaderChannels()
        {
            var parent = canvas;
            if (parent == null)
                return;
            parent.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            parent.rootCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            DestroyInstance();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_level == null)
                return;

            var area = _level.FieldRect;
            AddQuad(
                vh,
                Vector2.zero,
                area.size,
                new Vector4(0f, 0f, 0f, 0f), // uv0 = テクスチャの uv、z = 0(階の図)
                new Vector4(1f, 1f, 0f, 0f),
                new Vector4(area.width, area.height, 0f, 0f) // uv1 = 図の大きさ(u)
            );

            foreach (var stair in _level.Stairs)
                AddStairSteps(vh, stair, area.min);
        }

        // 階段の段の線を描く四角形。段の位置はシェーダーが、階段の左下からの位置で決める。
        private void AddStairSteps(VertexHelper vh, MinimapLayout.Stair stair, Vector2 origin)
        {
            var rect = stair.Rect;
            var alongX = Mathf.Abs(stair.Up.x) > Mathf.Abs(stair.Up.y);
            var length = alongX ? rect.width : rect.height;
            var count = Mathf.Max(1, Mathf.FloorToInt(length / _stepSpacing));
            var toward = alongX ? stair.Up.x : stair.Up.y;
            // uv1 = 階段の幅・高さ、段の数、段が並ぶ向きが X か
            var extra = new Vector4(rect.width, rect.height, count, alongX ? 1f : 0f);

            var min = rect.min - origin - Vector2.one * StairMargin;
            var size = rect.size + Vector2.one * (StairMargin * 2f);
            // uv0.xy = 階段の左下からの位置(四角形の左下は -StairMargin)、z = 段の四角形、w = 上る向き
            var start = new Vector4(-StairMargin, -StairMargin, 1f, toward > 0f ? 1f : -1f);
            var end = new Vector4(rect.width + StairMargin, rect.height + StairMargin, 1f, start.w);
            AddQuad(vh, min, size, start, end, extra);
        }

        // 左下 min・大きさ size の四角形。uv0 は左下で start、右上で end(間は補間)、uv1 は全頂点で同じ値。
        private void AddQuad(
            VertexHelper vh,
            Vector2 min,
            Vector2 size,
            Vector4 start,
            Vector4 end,
            Vector4 uv1
        )
        {
            Color32 c = color;
            var first = vh.currentVertCount;
            // 左下・左上・右上・右下の順
            for (var i = 0; i < 4; i++)
            {
                var tx = i >= 2 ? 1f : 0f;
                var ty = i == 1 || i == 2 ? 1f : 0f;
                var uv0 = new Vector4(
                    Mathf.Lerp(start.x, end.x, tx),
                    Mathf.Lerp(start.y, end.y, ty),
                    start.z,
                    start.w
                );
                vh.AddVert(
                    min + new Vector2(size.x * tx, size.y * ty),
                    c,
                    uv0,
                    uv1,
                    Vector3.zero,
                    Vector4.zero
                );
            }
            vh.AddTriangle(first, first + 1, first + 2);
            vh.AddTriangle(first + 2, first + 3, first);
        }
    }
}
