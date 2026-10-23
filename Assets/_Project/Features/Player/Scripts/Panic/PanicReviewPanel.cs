using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// ゲームの終了時に、検知が「焦り」と判定した場面をテストする人に見せ、
    /// 「焦っていた／焦っていなかった／覚えていない」を答えてもらう画面（PanicSessionLogger が付けて使う）。
    ///
    /// 人は戦闘中の経過時間を覚えていないので、場面は時刻ではなく、その瞬間の画像と出来事の言葉で示す。
    /// 表示中はゲームを止め、マウスのカーソルを出す。答えはボタンか 1・2・3 キー、Esc で答えずに終了できる。
    /// 画面は実行中に作り、シーンにもプレハブにも保存しない。
    /// </summary>
    public class PanicReviewPanel : MonoBehaviour
    {
        /// <summary>答えの種類。ファイルにもこの文字で書く。</summary>
        public const string AnswerPanicked = "焦っていた";
        public const string AnswerNotPanicked = "焦っていなかった";
        public const string AnswerDontRemember = "覚えていない";
        public const string AnswerSkipped = "未回答";

        // 終了前の振り返りは他のどの表示よりも手前に出す（記録中の表示は 200）
        private const int SortingOrder = 300;

        // 「ありがとうございました」を出してから閉じるまでの時間（秒）
        private const float ThanksSeconds = 1.5f;

        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.95f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.22f, 0.26f, 1f);
        private static readonly Color TextColor = new Color(0.95f, 0.95f, 0.95f, 1f);

        private IReadOnlyList<PanicMoment> _moments;
        private Action<PanicMoment, string> _onAnswered;
        private Action _onFinished;
        private int _current;
        private float _thanksRemaining = -1f;

        private GameObject _canvasObject;
        private RawImage _image;
        private TextMeshProUGUI _progressText;
        private TextMeshProUGUI _descriptionText;
        private GameObject _answerArea;

        private float _savedTimeScale;
        private CursorLockMode _savedLockState;
        private bool _savedCursorVisible;

        /// <summary>
        /// 振り返りを始める。答えるたびに onAnswered を呼び、全部終わる（または答えずに終了する）と onFinished を呼ぶ。
        /// </summary>
        public void Open(
            IReadOnlyList<PanicMoment> moments,
            Action<PanicMoment, string> onAnswered,
            Action onFinished
        )
        {
            _moments = moments;
            _onAnswered = onAnswered;
            _onFinished = onFinished;
            _current = 0;

            // 振り返りの間はゲームを止め、ボタンを押せるようカーソルを出す
            _savedTimeScale = Time.timeScale;
            _savedLockState = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            BuildCanvas();
            ShowCurrent();
        }

        private void Update()
        {
            if (_canvasObject == null)
                return;

            if (_thanksRemaining >= 0f)
            {
                _thanksRemaining -= Time.unscaledDeltaTime;
                if (_thanksRemaining < 0f)
                    Close();
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                Answer(AnswerPanicked);
            else if (
                keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame
            )
                Answer(AnswerNotPanicked);
            else if (
                keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame
            )
                Answer(AnswerDontRemember);
            else if (keyboard.escapeKey.wasPressedThisFrame)
                SkipRemaining();
        }

        private void Answer(string answer)
        {
            if (_thanksRemaining >= 0f || _current >= _moments.Count)
                return;

            _onAnswered?.Invoke(_moments[_current], answer);
            _current++;
            if (_current < _moments.Count)
                ShowCurrent();
            else
                ShowThanks();
        }

        // 残りを答えずに終える（答えなかった場面は「未回答」として残す）
        private void SkipRemaining()
        {
            if (_thanksRemaining >= 0f)
                return;

            for (; _current < _moments.Count; _current++)
            {
                _onAnswered?.Invoke(_moments[_current], AnswerSkipped);
            }
            ShowThanks();
        }

        private void ShowCurrent()
        {
            PanicMoment moment = _moments[_current];
            _image.texture = moment.Thumbnail;
            _image.color =
                moment.Thumbnail != null ? Color.white : new Color(0.15f, 0.15f, 0.15f, 1f);
            _progressText.text = $"全{_moments.Count}場面のうち {_current + 1} つ目";
            _descriptionText.text = $"{moment.ReviewText}\nこの場面で、あなたは焦っていましたか？";
        }

        private void ShowThanks()
        {
            _answerArea.SetActive(false);
            _image.gameObject.SetActive(false);
            _progressText.text = string.Empty;
            _descriptionText.text = "ご協力ありがとうございました。終了します。";
            _thanksRemaining = ThanksSeconds;
        }

        private void Close()
        {
            Destroy(_canvasObject);
            _canvasObject = null;

            Time.timeScale = _savedTimeScale;
            AudioListener.pause = false;
            Cursor.lockState = _savedLockState;
            Cursor.visible = _savedCursorVisible;

            _onFinished?.Invoke();
        }

        // ─── 画面の組み立て（1920×1080 を基準に、画面の大きさに合わせて拡大縮小する） ───

        private void BuildCanvas()
        {
            _canvasObject = new GameObject("PanicReviewCanvas");
            _canvasObject.hideFlags = HideFlags.DontSave;
            _canvasObject.transform.SetParent(transform, false);

            var canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _canvasObject.AddComponent<GraphicRaycaster>();

            Image background = CreateImage(_canvasObject.transform, "Background", PanelColor);
            Stretch(background.rectTransform);

            CreateText(
                _canvasObject.transform,
                "Title",
                "最後に、遊んでいたときの場面について教えてください",
                40f,
                new Vector2(0f, 430f),
                new Vector2(1700f, 70f)
            );
            _progressText = CreateText(
                _canvasObject.transform,
                "Progress",
                string.Empty,
                26f,
                new Vector2(0f, 370f),
                new Vector2(1200f, 40f)
            );

            var imageObject = new GameObject("Moment", typeof(RectTransform));
            imageObject.transform.SetParent(_canvasObject.transform, false);
            _image = imageObject.AddComponent<RawImage>();
            _image.rectTransform.anchoredPosition = new Vector2(0f, 80f);
            _image.rectTransform.sizeDelta = new Vector2(880f, 495f);

            _descriptionText = CreateText(
                _canvasObject.transform,
                "Description",
                string.Empty,
                32f,
                new Vector2(0f, -250f),
                new Vector2(1700f, 110f)
            );

            _answerArea = new GameObject("Answers", typeof(RectTransform));
            _answerArea.transform.SetParent(_canvasObject.transform, false);
            CreateButton(
                _answerArea.transform,
                "1  焦っていた",
                new Vector2(-460f, -380f),
                () => Answer(AnswerPanicked)
            );
            CreateButton(
                _answerArea.transform,
                "2  焦っていなかった",
                new Vector2(0f, -380f),
                () => Answer(AnswerNotPanicked)
            );
            CreateButton(
                _answerArea.transform,
                "3  覚えていない",
                new Vector2(460f, -380f),
                () => Answer(AnswerDontRemember)
            );
            CreateButton(
                _answerArea.transform,
                "答えずに終了する（Esc）",
                new Vector2(700f, -480f),
                SkipRemaining,
                new Vector2(400f, 60f),
                22f
            );
        }

        private static Image CreateImage(Transform parent, string objectName, Color color)
        {
            var imageObject = new GameObject(objectName, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string objectName,
            string content,
            float fontSize,
            Vector2 position,
            Vector2 size
        )
        {
            var textObject = new GameObject(objectName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            // 既定フォント（TMP Settings）は日本語を含む NotoSansJP なので、フォントは指定しない
            text.text = content;
            text.fontSize = fontSize;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = size;
            return text;
        }

        private static void CreateButton(
            Transform parent,
            string label,
            Vector2 position,
            UnityEngine.Events.UnityAction onClick,
            Vector2? size = null,
            float fontSize = 30f
        )
        {
            Image image = CreateImage(parent, label, ButtonColor);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size ?? new Vector2(420f, 80f);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            TextMeshProUGUI text = CreateText(
                image.transform,
                "Label",
                label,
                fontSize,
                Vector2.zero,
                image.rectTransform.sizeDelta
            );
            Stretch(text.rectTransform);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
