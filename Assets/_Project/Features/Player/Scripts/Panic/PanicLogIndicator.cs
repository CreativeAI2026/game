using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 焦り度ログを記録中であることを、テストする人に画面の隅で知らせる（PanicSessionLogger が付けて使う）。
    /// 記録できているか・どこに保存されるか・印の付け方を本人が確かめられるようにするため。
    ///
    /// 始めのしばらくは「記録中・保存先・印の付け方」を出し、その後は「● 記録中」だけを小さく残す
    /// （遊びの邪魔にならないように）。印を付けた時は短く「印を記録しました」と出す。
    /// 表示用のキャンバスは実行中に作り、シーンにもプレハブにも保存しない。
    /// </summary>
    public class PanicLogIndicator : MonoBehaviour
    {
        // 詳しい案内を出しておく時間（秒）
        private const float DetailSeconds = 8f;

        // 「印を記録しました」を出しておく時間（秒）
        private const float MarkerFeedbackSeconds = 1.5f;

        // 他の UI より手前に出す（焦り度のデバッグ表示は 100）
        private const int SortingOrder = 200;

        private const float DetailFontSize = 22f;
        private const float CompactFontSize = 18f;

        private TextMeshProUGUI _text;
        private string _detailMessage;
        private float _elapsed;
        private float _markerFeedbackRemaining;

        /// <summary>表示を作って出し始める。</summary>
        public void Show(string directory, string markerKeysLabel)
        {
            _detailMessage =
                "● 焦り度を記録中\n"
                + $"保存先: {directory}\n"
                + $"戦闘中に「焦った」と思ったら {markerKeysLabel}";
            _text = CreateText();
            _elapsed = 0f;
            Refresh();
        }

        /// <summary>印を付けた時に呼ぶ。</summary>
        public void NotifyMarker()
        {
            _markerFeedbackRemaining = MarkerFeedbackSeconds;
            Refresh();
        }

        private void Update()
        {
            if (_text == null)
                return;

            // 一時停止中（timeScale = 0）でも表示は進めたいので、実時間で数える
            _elapsed += Time.unscaledDeltaTime;
            _markerFeedbackRemaining = Mathf.Max(
                0f,
                _markerFeedbackRemaining - Time.unscaledDeltaTime
            );
            Refresh();
        }

        private void Refresh()
        {
            if (_markerFeedbackRemaining > 0f)
            {
                _text.fontSize = DetailFontSize;
                _text.text = "● 印を記録しました";
                _text.alpha = 1f;
                return;
            }

            bool isDetail = _elapsed < DetailSeconds;
            _text.fontSize = isDetail ? DetailFontSize : CompactFontSize;
            _text.text = isDetail ? _detailMessage : "● 記録中";
            _text.alpha = isDetail ? 1f : 0.6f;
        }

        private TextMeshProUGUI CreateText()
        {
            var canvasObject = new GameObject("PanicLogIndicatorCanvas");
            canvasObject.hideFlags = HideFlags.DontSave;
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var textObject = new GameObject("Text");
            textObject.transform.SetParent(canvasObject.transform, false);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            // 既定フォント（TMP Settings）は日本語を含む NotoSansJP なので、フォントは指定しない
            text.alignment = TextAlignmentOptions.TopRight;
            text.color = new Color(1f, 0.35f, 0.3f);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            // 右上に寄せる（左上の焦り度のデバッグ表示・右下の武器の HUD と重ならないように）
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -140f);
            rect.sizeDelta = new Vector2(1200f, 120f);
            return text;
        }
    }
}
