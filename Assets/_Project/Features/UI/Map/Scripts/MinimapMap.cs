using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// ミニマップの図。階ごとの距離フィールド(<see cref="MinimapLevelGraphic"/>)を並べて描く(同じ高さの別の建物は複数)。
    /// このオブジェクトの pivot をワールドの原点に当てるので、位置と拡大(1u = 何 px か)は呼び出し側が
    /// <see cref="RectTransform"/> の anchoredPosition と localScale で決める。
    /// 縁は距離フィールドから画面の 1px を基準に描くので、拡大・回転してもジャギーが出ない。色と線の太さの下限はここで決める。
    /// </summary>
    public sealed class MinimapMap : MonoBehaviour
    {
        [SerializeField, Tooltip("MinimapShape シェーダーのマテリアル")]
        private Material _material;

        [SerializeField]
        private Color _floorColor = new(0.27f, 0.32f, 0.39f, 1f);

        [SerializeField, Tooltip("床の外周(吹き抜け・建物の外との境目)の線")]
        private Color _edgeColor = new(0.5f, 0.57f, 0.66f, 1f);

        [SerializeField, Tooltip("壁のかたまりの地")]
        private Color _wallColor = new(0.13f, 0.16f, 0.2f, 1f);

        [SerializeField, Tooltip("壁が床に面する縁の線")]
        private Color _wallEdgeColor = new(0.84f, 0.88f, 0.93f, 1f);

        [SerializeField]
        private Color _glassColor = new(0.55f, 0.8f, 1f, 1f);

        [SerializeField]
        private Color _fenceColor = new(0.6f, 0.65f, 0.72f, 1f);

        [SerializeField]
        private Color _doorColor = new(0.42f, 0.86f, 0.62f, 1f);

        [SerializeField]
        private Color _stairsColor = new(0.32f, 0.37f, 0.45f, 1f);

        [SerializeField]
        private Color _stairsStepColor = new(0.7f, 0.76f, 0.84f, 1f);

        [SerializeField, Tooltip("床の縁取りの太さ(u)")]
        private float _edgeWidth = 0.5f;

        [SerializeField, Tooltip("階段の段の線の間隔(u)")]
        private float _stepSpacing = 0.9f;

        [SerializeField, Tooltip("階段の段の線の太さ(u)")]
        private float _stepWidth = 0.25f;

        [SerializeField, Tooltip("線の太さの下限(画面上の px)。これより細い線は、この太さで描く")]
        private float _minLinePixels = 1f;

        // シェーダーの設定名(毎回文字列で引かないよう、番号にしておく)
        private static readonly int FloorColorId = Shader.PropertyToID("_FloorColor");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int WallColorId = Shader.PropertyToID("_WallColor");
        private static readonly int WallEdgeColorId = Shader.PropertyToID("_WallEdgeColor");
        private static readonly int GlassColorId = Shader.PropertyToID("_GlassColor");
        private static readonly int FenceColorId = Shader.PropertyToID("_FenceColor");
        private static readonly int DoorColorId = Shader.PropertyToID("_DoorColor");
        private static readonly int StairsColorId = Shader.PropertyToID("_StairsColor");
        private static readonly int StairsStepColorId = Shader.PropertyToID("_StairsStepColor");
        private static readonly int SpreadId = Shader.PropertyToID("_Spread");
        private static readonly int MinLinePixelsId = Shader.PropertyToID("_MinLinePixels");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        private static readonly int StepWidthId = Shader.PropertyToID("_StepWidth");
        private static readonly int LineHalfId = Shader.PropertyToID("_LineHalf");

        private readonly List<MinimapLevelGraphic> _graphics = new();
        private Material _template;

        /// <summary>位置と拡大を決める RectTransform。</summary>
        public RectTransform RectTransform => (RectTransform)transform;

        /// <summary>
        /// 描く階(同じ高さの別の建物を並べて描くときは複数)。変わったときだけ呼ぶ。
        /// </summary>
        public void Show(IReadOnlyList<MinimapLayout.Level> levels)
        {
            var count = levels?.Count ?? 0;
            if (count > 0 && _template == null)
                _template = CreateTemplate();

            while (_graphics.Count < count)
                _graphics.Add(CreateGraphic());

            for (var i = 0; i < _graphics.Count; i++)
            {
                var graphic = _graphics[i];
                // 焼き込み前(テクスチャが無い)の階は描かない
                var drawn = i < count && levels[i].Field != null && levels[i].Ids != null;
                graphic.gameObject.SetActive(drawn);
                if (!drawn)
                {
                    if (i < count)
                        Debug.LogWarning(
                            $"[MinimapMap] {levels[i].Title} のテクスチャがありません。Tools > CreativeAI > Map > Bake Minimap を実行してください。",
                            this
                        );
                    continue;
                }
                graphic.Show(levels[i], _template, _stepSpacing);
                graphic.Place(RectTransform.pivot);
            }
        }

        private void OnDestroy()
        {
            if (_template != null)
                Destroy(_template);
        }

        private MinimapLevelGraphic CreateGraphic()
        {
            var go = new GameObject(
                "Level",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(MinimapLevelGraphic)
            );
            go.transform.SetParent(transform, false);
            var graphic = go.GetComponent<MinimapLevelGraphic>();
            graphic.raycastTarget = false;
            return graphic;
        }

        // 色と太さを、シェーダーのマテリアルの設定へ写した型(階ごとのマテリアルはこれを複製する)
        private Material CreateTemplate()
        {
            var template = new Material(_material) { hideFlags = HideFlags.HideAndDontSave };
            ApplySettings(template);
            return template;
        }

        // Play 中に Inspector で色や太さを変えたら、描いている階にすぐ反映する
        private void OnValidate()
        {
            if (!Application.isPlaying || _template == null)
                return;
            ApplySettings(_template);
            foreach (var graphic in _graphics)
                if (graphic.isActiveAndEnabled)
                    graphic.Refresh(_template, _stepSpacing);
        }

        private void ApplySettings(Material template)
        {
            template.SetColor(FloorColorId, _floorColor);
            template.SetColor(EdgeColorId, _edgeColor);
            template.SetColor(WallColorId, _wallColor);
            template.SetColor(WallEdgeColorId, _wallEdgeColor);
            template.SetColor(GlassColorId, _glassColor);
            template.SetColor(FenceColorId, _fenceColor);
            template.SetColor(DoorColorId, _doorColor);
            template.SetColor(StairsColorId, _stairsColor);
            template.SetColor(StairsStepColorId, _stairsStepColor);
            template.SetFloat(SpreadId, MinimapField.Spread);
            template.SetFloat(MinLinePixelsId, _minLinePixels);
            template.SetFloat(EdgeWidthId, _edgeWidth);
            template.SetFloat(StepWidthId, _stepWidth);
            template.SetVector(
                LineHalfId,
                new Vector4(
                    MinimapField.LineWidth(MinimapField.LineKind.WallEdge) * 0.5f,
                    MinimapField.LineWidth(MinimapField.LineKind.Fence) * 0.5f,
                    MinimapField.LineWidth(MinimapField.LineKind.Glass) * 0.5f,
                    MinimapField.LineWidth(MinimapField.LineKind.Door) * 0.5f
                )
            );
        }
    }
}
