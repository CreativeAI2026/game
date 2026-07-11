using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI
{
    public partial class ItemDetailPanel
    {
        private void TypeText(TMP_Text target, string text)
        {
            if (target == null)
                return;

            DOTween.Kill(target);
            text ??= string.Empty;
            target.text = text;
            target.ForceMeshUpdate();

            int characterCount = target.textInfo.characterCount;
            if (characterCount <= 0)
            {
                target.maxVisibleCharacters = 0;
                return;
            }

            target.maxVisibleCharacters = 0;
            float duration = characterCount / Mathf.Max(1f, _charactersPerSecond);

            DOTween
                .To(
                    () => 0f,
                    value =>
                        target.maxVisibleCharacters = Mathf.Clamp(
                            Mathf.FloorToInt(value),
                            0,
                            characterCount
                        ),
                    characterCount,
                    duration
                )
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .SetTarget(target)
                .OnComplete(() => target.maxVisibleCharacters = characterCount);
        }

        private void PlayIconSpin()
        {
            if (_icon == null || _icon.sprite == null)
                return;

            var iconRect = _icon.rectTransform;
            iconRect.DOKill();
            iconRect.localRotation = Quaternion.identity;

            iconRect
                .DORotate(new Vector3(0f, 360f, 0f), _iconSpinDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.OutQuint)
                .SetUpdate(true);
        }
    }
}
