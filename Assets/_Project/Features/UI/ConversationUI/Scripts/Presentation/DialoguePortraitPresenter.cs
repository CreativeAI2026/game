using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>立ち絵の解決、左右スロット、話者フォーカスと立ち絵演出を担当する。</summary>
    internal sealed class DialoguePortraitPresenter
    {
        internal readonly struct ResolvedPortrait
        {
            public ResolvedPortrait(
                Sprite sprite,
                Sprite icon,
                DialoguePortraitSide side,
                string displayName,
                Color themeColor,
                AudioClip typingSound,
                Vector2 offset
            )
            {
                Sprite = sprite;
                Icon = icon;
                Side = side;
                DisplayName = displayName ?? string.Empty;
                ThemeColor = themeColor;
                TypingSound = typingSound;
                Offset = offset;
            }

            public Sprite Sprite { get; }
            public Sprite Icon { get; }
            public DialoguePortraitSide Side { get; }
            public string DisplayName { get; }
            public Color ThemeColor { get; }
            public AudioClip TypingSound { get; }
            public Vector2 Offset { get; }
        }

        private Image _left;
        private Image _right;
        private Image _baseLeft;
        private Image _baseRight;
        private float _leftAnchorX;
        private float _rightAnchorX;
        private float _leftBaseTopY;
        private float _rightBaseTopY;
        private Vector2 _leftOffset;
        private Vector2 _rightOffset;
        private float _activeScale;
        private float _inactiveScale;
        private float _inactiveBrightness;
        private float _fadeDuration;
        private float _focusDuration;
        private bool _leftShown;
        private bool _rightShown;
        private bool _leftObscured;
        private bool _rightObscured;
        private DialoguePortraitSide _activeSide;

        public Image RightPortrait => _right;

        public void Configure(
            Image left,
            Image right,
            float leftAnchorX,
            float rightAnchorX,
            float activeScale,
            float inactiveScale,
            float inactiveBrightness,
            float fadeDuration,
            float focusDuration
        )
        {
            bool leftChanged = left != _baseLeft;
            bool rightChanged = right != _baseRight;
            _left = left;
            _right = right;
            _leftAnchorX = leftAnchorX;
            _rightAnchorX = rightAnchorX;
            if (leftChanged)
            {
                _baseLeft = left;
                _leftBaseTopY = ResolveTopY(left);
            }
            if (rightChanged)
            {
                _baseRight = right;
                _rightBaseTopY = right != null ? ResolveTopY(right) : _leftBaseTopY;
            }
            _activeScale = activeScale;
            _inactiveScale = inactiveScale;
            _inactiveBrightness = inactiveBrightness;
            _fadeDuration = fadeDuration;
            _focusDuration = focusDuration;
        }

        public IEnumerator Set(ResolvedPortrait resolved)
        {
            if (_left == null)
                yield break;
            EnsureSlots();
            if (resolved.Sprite == null)
            {
                yield return FadeOutAll();
                yield break;
            }

            var active = resolved.Side == DialoguePortraitSide.Left ? _left : _right;
            var inactive = resolved.Side == DialoguePortraitSide.Left ? _right : _left;
            _activeSide = resolved.Side;
            bool wasShown = resolved.Side == DialoguePortraitSide.Left ? _leftShown : _rightShown;
            // An expression change on an occupied side is not another entrance. Treating it as
            // new caused reveal -> brighten -> grow to play as two visibly separate motions.
            bool isNew = !wasShown;
            active.sprite = resolved.Sprite;
            active.enabled = true;
            SetOffset(resolved.Side, resolved.Offset);
            ApplySide(active, resolved.Side);
            if (resolved.Side == DialoguePortraitSide.Left)
                _leftShown = true;
            else
                _rightShown = true;

            Color activeTarget = TargetColor(resolved.Side, true);
            Color activeStart = isNew
                ? new Color(activeTarget.r, activeTarget.g, activeTarget.b, 0f)
                : active.color;
            float activeScaleStart = isNew
                ? _inactiveScale
                : Mathf.Abs(active.rectTransform.localScale.x);
            Color inactiveStart = inactive != null ? inactive.color : Color.white;
            float inactiveScaleStart =
                inactive != null ? Mathf.Abs(inactive.rectTransform.localScale.x) : _inactiveScale;
            float duration = isNew ? _fadeDuration : _focusDuration;
            for (
                float elapsed = 0f;
                elapsed < duration;
                elapsed += Mathf.Max(Time.unscaledDeltaTime, 1f / 60f)
            )
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.01f, duration));
                active.color = Color.Lerp(activeStart, activeTarget, t);
                SetScale(active, resolved.Side, Mathf.Lerp(activeScaleStart, _activeScale, t));
                if (inactive != null && inactive.enabled)
                {
                    var target = TargetColor(Opposite(resolved.Side), false);
                    inactive.color = Color.Lerp(inactiveStart, target, t);
                    SetScale(
                        inactive,
                        Opposite(resolved.Side),
                        Mathf.Lerp(inactiveScaleStart, _inactiveScale, t)
                    );
                }
                yield return null;
            }
            active.color = activeTarget;
            SetScale(active, resolved.Side, _activeScale);
            if (inactive != null && inactive.enabled)
            {
                inactive.color = TargetColor(Opposite(resolved.Side), false);
                SetScale(inactive, Opposite(resolved.Side), _inactiveScale);
            }
        }

        public void SetVisible(DialoguePortraitSide side, bool visible)
        {
            var portrait = side == DialoguePortraitSide.Left ? _left : _right;
            if (portrait != null)
                portrait.enabled = visible && portrait.sprite != null;
        }

        public void HideImmediate()
        {
            if (_left != null)
            {
                _left.enabled = false;
                _leftShown = false;
            }
            if (_right != null)
            {
                _right.enabled = false;
                _rightShown = false;
            }
        }

        public bool IsObscured(DialoguePortraitSide side) =>
            side == DialoguePortraitSide.Left ? _leftObscured : _rightObscured;

        public IEnumerator SetObscured(
            DialoguePortraitSide side,
            bool obscured,
            float duration = 0.5f
        )
        {
            if (side == DialoguePortraitSide.Left)
                _leftObscured = obscured;
            else
                _rightObscured = obscured;

            EnsureSlots();
            var portrait = side == DialoguePortraitSide.Left ? _left : _right;
            if (portrait == null || !portrait.enabled)
                yield break;

            Color start = portrait.color;
            Color target = TargetColor(side, side == _activeSide);
            duration = Mathf.Max(0.01f, duration);
            for (
                float elapsed = 0f;
                elapsed < duration;
                elapsed += Mathf.Max(Time.unscaledDeltaTime, 1f / 60f)
            )
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                portrait.color = Color.Lerp(start, target, t);
                yield return null;
            }
            portrait.color = target;
        }

        public IEnumerator PlayEffect(
            DialoguePortraitSide side,
            ConversationView.PortraitEffect effect,
            float duration
        )
        {
            var portrait = side == DialoguePortraitSide.Left ? _left : _right;
            if (portrait == null || !portrait.enabled)
                yield break;
            var rect = portrait.rectTransform;
            Vector2 basePosition = rect.anchoredPosition;
            Color baseColor = portrait.color;
            duration = Mathf.Max(0.01f, duration);
            for (
                float elapsed = 0f;
                elapsed < duration;
                elapsed += Mathf.Max(Time.unscaledDeltaTime, 1f / 60f)
            )
            {
                float t = elapsed / duration;
                if (effect == ConversationView.PortraitEffect.Shake)
                    rect.anchoredPosition =
                        basePosition + Vector2.right * Mathf.Sin(t * Mathf.PI * 8f) * 10f;
                else if (effect == ConversationView.PortraitEffect.Jump)
                    rect.anchoredPosition =
                        basePosition + Vector2.up * Mathf.Sin(t * Mathf.PI) * 28f;
                else if (effect == ConversationView.PortraitEffect.Fade)
                    portrait.color = new Color(
                        baseColor.r,
                        baseColor.g,
                        baseColor.b,
                        Mathf.Abs(Mathf.Cos(t * Mathf.PI))
                    );
                yield return null;
            }
            rect.anchoredPosition = basePosition;
            portrait.color = baseColor;
        }

        public IEnumerator FadeOutAll(float duration)
        {
            Color leftStart = _left != null ? _left.color : Color.clear;
            Color rightStart = _right != null ? _right.color : Color.clear;
            duration = Mathf.Max(0.01f, duration);
            for (float elapsed = 0f; elapsed < duration; elapsed += FrameDelta())
            {
                float alpha = 1f - Mathf.SmoothStep(0f, 1f, elapsed / duration);
                if (_left != null && _left.enabled)
                    _left.color = new Color(
                        leftStart.r,
                        leftStart.g,
                        leftStart.b,
                        leftStart.a * alpha
                    );
                if (_right != null && _right.enabled)
                    _right.color = new Color(
                        rightStart.r,
                        rightStart.g,
                        rightStart.b,
                        rightStart.a * alpha
                    );
                yield return null;
            }
            if (_left != null && _left.enabled)
                _left.color = new Color(leftStart.r, leftStart.g, leftStart.b, 0f);
            if (_right != null && _right.enabled)
                _right.color = new Color(rightStart.r, rightStart.g, rightStart.b, 0f);
        }

        public void EnsureSlots()
        {
            if (_left == null)
                return;
            if (_right == null)
            {
                ApplySide(_left, DialoguePortraitSide.Left);
                _right = Object.Instantiate(_left, _left.transform.parent);
                _right.name = "PortraitRight";
                _right.transform.SetSiblingIndex(_left.transform.GetSiblingIndex() + 1);
                _right.enabled = false;
                SetScale(_right, DialoguePortraitSide.Right, 1f);
            }
            ApplySide(_right, DialoguePortraitSide.Right);
        }

        private IEnumerator FadeOutAll()
        {
            bool leftVisible = _left != null && _left.enabled;
            bool rightVisible = _right != null && _right.enabled;
            if (!leftVisible && !rightVisible)
                yield break;

            Color leftStart = leftVisible ? _left.color : Color.clear;
            Color rightStart = rightVisible ? _right.color : Color.clear;
            float leftScale = leftVisible ? Mathf.Abs(_left.rectTransform.localScale.x) : 1f;
            float rightScale = rightVisible ? Mathf.Abs(_right.rectTransform.localScale.x) : 1f;
            float duration = Mathf.Max(0.12f, _fadeDuration * 0.75f);
            for (float elapsed = 0f; elapsed < duration; elapsed += FrameDelta())
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                if (leftVisible)
                {
                    _left.color = new Color(leftStart.r, leftStart.g, leftStart.b, 1f - t);
                    SetScale(
                        _left,
                        DialoguePortraitSide.Left,
                        Mathf.Lerp(leftScale, leftScale * 0.96f, t)
                    );
                }
                if (rightVisible)
                {
                    _right.color = new Color(rightStart.r, rightStart.g, rightStart.b, 1f - t);
                    SetScale(
                        _right,
                        DialoguePortraitSide.Right,
                        Mathf.Lerp(rightScale, rightScale * 0.96f, t)
                    );
                }
                yield return null;
            }

            if (_left != null)
                _left.enabled = false;
            if (_right != null)
                _right.enabled = false;
            _leftShown = false;
            _rightShown = false;
        }

        private void ApplySide(Image portrait, DialoguePortraitSide side)
        {
            var rect = portrait.rectTransform;
            float anchorX = side == DialoguePortraitSide.Left ? _leftAnchorX : _rightAnchorX;
            float baseTopY = side == DialoguePortraitSide.Left ? _leftBaseTopY : _rightBaseTopY;
            Vector2 offset = side == DialoguePortraitSide.Left ? _leftOffset : _rightOffset;
            rect.anchorMin = new Vector2(anchorX, rect.anchorMin.y);
            rect.anchorMax = new Vector2(anchorX, rect.anchorMax.y);
            rect.pivot = new Vector2(rect.pivot.x, 1f);
            rect.anchoredPosition = new Vector2(offset.x, baseTopY + offset.y);
        }

        private void SetScale(Image portrait, DialoguePortraitSide side, float magnitude)
        {
            var rect = portrait.rectTransform;
            rect.localScale = new Vector3(
                (side == DialoguePortraitSide.Left ? 1f : -1f) * magnitude,
                magnitude,
                rect.localScale.z
            );
        }

        private void SetOffset(DialoguePortraitSide side, Vector2 offset)
        {
            if (side == DialoguePortraitSide.Left)
                _leftOffset = offset;
            else
                _rightOffset = offset;
        }

        private static float ResolveTopY(Image portrait)
        {
            if (portrait == null)
                return 0f;
            var rect = portrait.rectTransform;
            return rect.anchoredPosition.y + (1f - rect.pivot.y) * rect.rect.height;
        }

        private static DialoguePortraitSide Opposite(DialoguePortraitSide side) =>
            side == DialoguePortraitSide.Left
                ? DialoguePortraitSide.Right
                : DialoguePortraitSide.Left;

        private Color TargetColor(DialoguePortraitSide side, bool active)
        {
            bool obscured = side == DialoguePortraitSide.Left ? _leftObscured : _rightObscured;
            if (obscured)
                return new Color(0.035f, 0.045f, 0.065f, 1f);
            if (active)
                return Color.white;
            return new Color(_inactiveBrightness, _inactiveBrightness, _inactiveBrightness, 1f);
        }

        private static float FrameDelta() => Mathf.Max(Time.unscaledDeltaTime, 1f / 60f);
    }
}
