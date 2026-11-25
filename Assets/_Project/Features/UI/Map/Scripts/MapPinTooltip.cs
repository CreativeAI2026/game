using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 全画面マップで、ピンにマウスを乗せたときにマウスの右下へ出す説明(階と現在地からの距離)。
    /// </summary>
    internal sealed class MapPinTooltip
    {
        private const float FontSize = 22f;

        private readonly RectTransform _area;
        private readonly RectTransform _tooltip;
        private readonly TMP_Text _text;
        private long _key = -1; // 説明の文字を作り直すかの判定(階と距離)

        /// <summary>説明の枠を area の直下に作る(最初は隠す)。地図の表示範囲で切り抜かれないよう、画面の直下に置く。</summary>
        public MapPinTooltip(RectTransform area, TMP_FontAsset font)
        {
            _area = area;
            var go = new GameObject("PinTooltip", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(area, false);
            var background = go.GetComponent<Image>();
            background.color = new Color(0.05f, 0.07f, 0.1f, 0.92f);
            background.raycastTarget = false;
            _tooltip = (RectTransform)go.transform;
            _tooltip.pivot = new Vector2(0f, 1f);

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(go.transform, false);
            _text = textObject.GetComponent<TextMeshProUGUI>();
            if (font != null)
                _text.font = font;
            _text.fontSize = FontSize;
            _text.color = Color.white;
            _text.raycastTarget = false;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            var textRect = _text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 6f);
            textRect.offsetMax = new Vector2(-12f, -6f);
            go.SetActive(false);
        }

        /// <param name="title">ピンのある階の名前</param>
        /// <param name="meters">現在地からの距離。負なら距離を出さない</param>
        public void Show(Vector2 pointer, Camera eventCamera, string title, int meters, int level)
        {
            // 階か距離が替わったときだけ、文字を作り直す
            var key = level * 100000L + meters;
            if (key != _key || !_tooltip.gameObject.activeSelf)
            {
                _key = key;
                _text.text = meters >= 0 ? $"{title}\n現在地から {meters}m" : title;
                _tooltip.gameObject.SetActive(true);
                _tooltip.SetAsLastSibling(); // 出すときに1度だけ、他の部品より手前へ
                var size = _text.GetPreferredValues(_text.text);
                _tooltip.sizeDelta = new Vector2(size.x + 24f, size.y + 12f);
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _area,
                pointer,
                eventCamera,
                out var local
            );
            // マウスの右下に出し、画面の端からはみ出すなら内側へ寄せる
            var rect = _area.rect;
            var position = local + new Vector2(16f, -16f);
            position.x = Mathf.Min(position.x, rect.xMax - _tooltip.sizeDelta.x);
            position.y = Mathf.Max(position.y, rect.yMin + _tooltip.sizeDelta.y);
            _tooltip.anchorMin = _tooltip.anchorMax = new Vector2(0.5f, 0.5f);
            _tooltip.anchoredPosition = position - rect.center; // 中心アンカーからの位置に直す
        }

        public void Hide() => _tooltip.gameObject.SetActive(false);
    }
}
