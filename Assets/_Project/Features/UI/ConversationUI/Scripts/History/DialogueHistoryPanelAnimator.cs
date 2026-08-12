using System;
using System.Collections;
using UnityEngine;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>履歴パネルのフェード開閉と終了通知を管理する。</summary>
    internal sealed class DialogueHistoryPanelAnimator
    {
        private readonly MonoBehaviour _owner;
        private GameObject _panel;
        private CanvasGroup _group;
        private Action _closed;
        private Coroutine _animation;

        public DialogueHistoryPanelAnimator(MonoBehaviour owner) => _owner = owner;

        public void Configure(GameObject panel, CanvasGroup group, Action closed)
        {
            _panel = panel;
            _group = group;
            _closed = closed;
        }

        public void Play(bool opening)
        {
            if (_animation != null)
                _owner.StopCoroutine(_animation);
            _animation = _owner.StartCoroutine(Animate(opening));
        }

        private IEnumerator Animate(bool opening)
        {
            float start = _group != null ? _group.alpha : (opening ? 0f : 1f);
            float target = opening ? 1f : 0f;
            float elapsed = 0f;
            const float duration = 0.18f;
            while (elapsed < duration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                if (_group != null)
                    _group.alpha = Mathf.Lerp(start, target, t);
                elapsed += Mathf.Max(Time.unscaledDeltaTime, 1f / 60f);
                yield return null;
            }

            if (_group != null)
                _group.alpha = target;
            if (!opening)
            {
                _panel.SetActive(false);
                _closed?.Invoke();
            }
            _animation = null;
        }
    }
}
