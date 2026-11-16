using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 会話の背景まわりの演出面。会話UIルートの子として実行時に生成する。
    /// 下から 背景画像 → 色の幕(暗転) → (立ち絵・ウィンドウ) → フラッシュ の順に重なる。
    /// </summary>
    internal sealed class DialogueBackdropPresenter
    {
        public const float MaxFadeSeconds = 10f;

        private readonly Transform _anchor; // 会話UIルート。背面の層はこの直前に差し込む
        private readonly Transform _parent;
        private Image _picture;
        private Image _fill;
        private Image _flash;

        /// <param name="anchor">
        /// 会話UIルート。会話UIは非会話中 alpha=0 なので、黒幕はその内側ではなく親の直下・ルートの背面に置く
        /// (内側だと会話UIの出現に合わせて黒もフェードインし、開始直後に背景が一瞬見えてしまう)。
        /// </param>
        public DialogueBackdropPresenter(Transform anchor)
        {
            _anchor = anchor;
            _parent = anchor.parent != null ? anchor.parent : anchor;
        }

        /// <summary>色の幕を即座に不透明にする。</summary>
        public void SetColor(Color color)
        {
            Image fill = EnsureFill();
            fill.color = new Color(color.r, color.g, color.b, 1f);
            fill.gameObject.SetActive(true);
        }

        /// <summary>色の幕を、その色へフェードして覆う。</summary>
        public IEnumerator FadeTo(Color color, float seconds)
        {
            Image fill = EnsureFill();
            Color from = fill.gameObject.activeSelf
                ? fill.color
                : new Color(color.r, color.g, color.b, 0f);
            yield return Fade(fill, from, new Color(color.r, color.g, color.b, 1f), seconds);
        }

        /// <summary>色の幕が薄れて消えていく。幕が出ていなければ何もしない。</summary>
        public IEnumerator FadeToClear(float seconds)
        {
            if (_fill == null || !_fill.gameObject.activeSelf)
                yield break;
            Color from = _fill.color;
            yield return Fade(_fill, from, new Color(from.r, from.g, from.b, 0f), seconds);
        }

        /// <summary>画面全体を一瞬その色に光らせ、薄れて消える(立ち絵やウィンドウの上に重なる)。</summary>
        public IEnumerator Flash(Color color, float seconds)
        {
            Image flash = EnsureFlash();
            flash.rectTransform.SetAsLastSibling();
            yield return Fade(
                flash,
                new Color(color.r, color.g, color.b, 1f),
                new Color(color.r, color.g, color.b, 0f),
                seconds
            );
        }

        /// <summary>背景画像を差し替える(フェードで現れる)。</summary>
        public IEnumerator ShowPicture(Sprite sprite, float seconds)
        {
            Image picture = EnsurePicture();
            picture.sprite = sprite;
            yield return Fade(picture, new Color(1f, 1f, 1f, 0f), Color.white, seconds);
        }

        /// <summary>背景画像を消す。</summary>
        public IEnumerator ClearPicture(float seconds)
        {
            if (_picture == null || !_picture.gameObject.activeSelf)
                yield break;
            yield return Fade(_picture, _picture.color, new Color(1f, 1f, 1f, 0f), seconds);
        }

        /// <summary>会話の終わりに演出面を片付ける(次の会話に持ち越さない)。</summary>
        public void Reset()
        {
            foreach (Image image in new[] { _picture, _fill, _flash })
                if (image != null)
                    image.gameObject.SetActive(false);
        }

        private static IEnumerator Fade(Image image, Color from, Color to, float seconds)
        {
            seconds = Mathf.Clamp(seconds, 0f, MaxFadeSeconds);
            image.gameObject.SetActive(true);
            image.color = from;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                image.color = Color.Lerp(from, to, elapsed / seconds);
                yield return null;
            }
            image.color = to;
            if (to.a <= 0f)
                image.gameObject.SetActive(false);
        }

        private Image EnsurePicture() =>
            _picture != null ? _picture : _picture = Create("BackdropPicture", true);

        private Image EnsureFill() => _fill != null ? _fill : _fill = Create("BackdropFill", true);

        private Image EnsureFlash() =>
            _flash != null ? _flash : _flash = Create("BackdropFlash", false);

        /// <summary>
        /// 全画面の Image を作る。behind なら会話UIルートの背面(背景画像は色の幕のさらに下)、そうでなければ最前面に置く。
        /// </summary>
        private Image Create(string name, bool behind)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_parent, false);
            if (!behind)
                rect.SetAsLastSibling();
            else
            {
                Transform above = _fill != null && _picture == null ? _fill.transform : _anchor;
                rect.SetSiblingIndex(above.GetSiblingIndex());
            }
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0f);
            go.SetActive(false);
            return image;
        }
    }
}
