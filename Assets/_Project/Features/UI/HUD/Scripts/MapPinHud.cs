using System.Collections.Generic;
using CreativeAI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// マップピンの距離表示と、画面の外にあるピンの方向を示す矢印。
    /// 画面内のピンには頭の上に「23m」と出し、画面外のピンには画面の端に矢印と距離を出す。
    /// UIRoot の子として常駐し、ピンが無いときは何も出さない。
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class MapPinHud : MonoBehaviour
    {
        private const float EdgeMargin = 0.08f; // 画面の端から矢印までの余白(短辺に対する割合)
        private const float ArrowSizeRatio = 0.05f; // 矢印の大きさ(画面の高さに対する割合)
        private const float FontSizeRatio = 0.03f;
        private static readonly Color ArrowColor = new(1f, 0.8f, 0.15f, 0.95f);

        private sealed class Marker
        {
            public RectTransform Root;
            public RectTransform Arrow;
            public TextMeshProUGUI Label;
        }

        [Tooltip("右向き(+x)の矢じり")]
        [SerializeField]
        private Sprite _arrowSprite;

        private readonly List<Marker> _markers = new();
        private RectTransform _canvasRoot;
        private Canvas _canvas;

        private void Awake()
        {
            _canvasRoot = (RectTransform)transform;
            _canvas = GetComponent<Canvas>();
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            var shownCount = 0;
            if (cam != null)
            {
                var pins = MapPin.Active;
                for (var i = 0; i < pins.Count; i++)
                {
                    var pin = pins[i];
                    if (pin == null || !pin.Shown)
                        continue;
                    Place(Get(shownCount), pin, cam, _canvas.scaleFactor);
                    shownCount++;
                }
            }

            for (var i = shownCount; i < _markers.Count; i++)
                _markers[i].Root.gameObject.SetActive(false);
        }

        private Marker Get(int index)
        {
            while (_markers.Count <= index)
                _markers.Add(CreateMarker());
            return _markers[index];
        }

        private Marker CreateMarker()
        {
            var root = new GameObject("MapPinMarker", typeof(RectTransform));
            root.transform.SetParent(_canvasRoot, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = Vector2.zero;

            var arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
            arrowGo.transform.SetParent(root.transform, false);
            var image = arrowGo.GetComponent<Image>();
            image.sprite = _arrowSprite;
            image.color = ArrowColor;
            image.raycastTarget = false;

            var labelGo = new GameObject(
                "Distance",
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            );
            labelGo.transform.SetParent(root.transform, false);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.outlineColor = new Color32(0, 0, 0, 255);
            label.outlineWidth = 0.25f;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            return new Marker
            {
                Root = rootRect,
                Arrow = arrowGo.GetComponent<RectTransform>(),
                Label = label,
            };
        }

        /// <param name="scale">画面のピクセルからキャンバスの単位へ直す倍率(CanvasScaler の拡大率)</param>
        private static void Place(Marker marker, MapPin pin, Camera cam, float scale)
        {
            var width = Screen.width / scale;
            var height = Screen.height / scale;
            var margin = Mathf.Min(width, height) * EdgeMargin;
            var arrowSize = height * ArrowSizeRatio;

            var player = MapPin.PlayerTransform;
            var origin = player != null ? player.position : cam.transform.position;
            var meters = Vector3.Distance(origin, pin.Anchor);

            var screenTop = cam.WorldToScreenPoint(pin.Top);
            var top = new Vector3(screenTop.x / scale, screenTop.y / scale, screenTop.z);
            var onScreen =
                top.z > 0f
                && top.x >= margin
                && top.x <= width - margin
                && top.y >= margin
                && top.y <= height - margin;

            marker.Root.gameObject.SetActive(true);
            marker.Arrow.gameObject.SetActive(!onScreen);
            marker.Label.fontSize = height * FontSizeRatio;
            marker.Label.SetText("{0}m", Mathf.RoundToInt(meters));
            marker.Label.rectTransform.sizeDelta = new Vector2(arrowSize * 4f, arrowSize);

            if (onScreen)
            {
                marker.Root.anchoredPosition = top;
                marker.Label.rectTransform.anchoredPosition = Vector2.zero;
                return;
            }

            // 画面の外: 中心から見たピンの向きに沿って、画面の端へ矢印を置く。
            var center = new Vector2(width, height) * 0.5f;
            var direction = (Vector2)top - center;
            if (top.z < 0f)
                direction = -direction; // カメラの後ろにあるときは、画面上の向きが逆になる
            if (direction.sqrMagnitude < 1f)
                direction = Vector2.down;

            var halfWidth = width * 0.5f - margin;
            var halfHeight = height * 0.5f - margin;
            var t = Mathf.Min(
                halfWidth / Mathf.Max(Mathf.Abs(direction.x), 0.0001f),
                halfHeight / Mathf.Max(Mathf.Abs(direction.y), 0.0001f)
            );
            marker.Root.anchoredPosition = center + direction * t;

            marker.Arrow.sizeDelta = Vector2.one * arrowSize;
            marker.Arrow.anchoredPosition = Vector2.zero;
            marker.Arrow.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
            );

            // 距離は矢印の内側(画面の中心寄り)に置く。端に出ると切れるため。
            marker.Label.rectTransform.anchoredPosition =
                -(arrowSize * 1.1f) * direction.normalized;
        }
    }
}
