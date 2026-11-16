using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// TMPのレイアウトを確定したまま、表示文字数だけを増やすタイプライター演出。
    /// </summary>
    public sealed class TmpTypewriterPlayer
    {
        private readonly MonoBehaviour _owner;
        private readonly Dictionary<TMP_Text, Coroutine> _animations = new();

        public TmpTypewriterPlayer(MonoBehaviour owner)
        {
            _owner = owner;
        }

        public void Play(
            TMP_Text text,
            string content,
            float startDelay,
            float characterInterval,
            bool animate = true
        )
        {
            if (text == null)
                return;

            Stop(text);
            text.text = content ?? string.Empty;
            text.maxVisibleCharacters = int.MaxValue;

            if (
                !animate
                || !Application.isPlaying
                || !_owner.isActiveAndEnabled
                || string.IsNullOrEmpty(text.text)
                || characterInterval <= 0f
            )
                return;

            text.ForceMeshUpdate(true, true);
            int characterCount = text.textInfo.characterCount;
            if (characterCount <= 0)
                return;

            text.maxVisibleCharacters = 0;
            _animations[text] = _owner.StartCoroutine(
                Reveal(text, characterCount, Mathf.Max(0f, startDelay), characterInterval)
            );
        }

        public void CompleteAll()
        {
            foreach (KeyValuePair<TMP_Text, Coroutine> animation in _animations)
            {
                if (animation.Value != null)
                    _owner.StopCoroutine(animation.Value);
                if (animation.Key != null)
                    animation.Key.maxVisibleCharacters = int.MaxValue;
            }

            _animations.Clear();
        }

        private IEnumerator Reveal(
            TMP_Text text,
            int characterCount,
            float startDelay,
            float characterInterval
        )
        {
            float elapsed = 0f;
            while (elapsed < startDelay)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            float revealElapsed = 0f;
            int visibleCharacters = 0;
            while (visibleCharacters < characterCount && text != null)
            {
                revealElapsed += Time.unscaledDeltaTime;
                int nextVisible = Mathf.Min(
                    characterCount,
                    Mathf.FloorToInt(revealElapsed / characterInterval) + 1
                );

                if (nextVisible != visibleCharacters)
                {
                    visibleCharacters = nextVisible;
                    text.maxVisibleCharacters = visibleCharacters;
                }

                yield return null;
            }

            if (text != null)
            {
                text.maxVisibleCharacters = int.MaxValue;
                _animations.Remove(text);
            }
        }

        private void Stop(TMP_Text text)
        {
            if (!_animations.TryGetValue(text, out Coroutine animation))
                return;

            if (animation != null)
                _owner.StopCoroutine(animation);
            _animations.Remove(text);
        }
    }
}
