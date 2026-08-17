using System;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>履歴パネルの最新位置追従とスクロール位置表示を管理する。</summary>
    internal sealed class DialogueHistoryScrollController
    {
        private ScrollRect _scrollRect;
        private RectTransform _content;
        private GameObject _latestButton;
        private Image _scrollIndicator;
        private Func<bool> _isOpen;

        public void Configure(
            ScrollRect scrollRect,
            RectTransform content,
            GameObject latestButton,
            Image scrollIndicator,
            Func<bool> isOpen
        )
        {
            _scrollRect = scrollRect;
            _content = content;
            _latestButton = latestButton;
            _scrollIndicator = scrollIndicator;
            _isOpen = isOpen;
        }

        public bool ShouldFollowLatest() =>
            !(_isOpen?.Invoke() ?? false)
            || _scrollRect == null
            || _scrollRect.verticalNormalizedPosition <= 0.03f;

        public void ScrollToLatest()
        {
            if (_scrollRect == null || _content == null)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _scrollRect.verticalNormalizedPosition = 0f;
            Refresh();
        }

        public void Refresh()
        {
            RefreshLatestButton();
            RefreshIndicator();
        }

        public void RefreshLatestButton()
        {
            if (_latestButton == null || _scrollRect == null)
                return;

            _latestButton.SetActive(
                (_isOpen?.Invoke() ?? false) && _scrollRect.verticalNormalizedPosition > 0.03f
            );
        }

        private void RefreshIndicator()
        {
            if (_scrollIndicator == null || _scrollRect == null || _content == null)
                return;

            float viewportHeight = Mathf.Max(1f, _scrollRect.viewport.rect.height);
            float contentHeight = Mathf.Max(viewportHeight, _content.rect.height);
            float size = Mathf.Clamp(viewportHeight / contentHeight, 0.08f, 1f);
            float start = (1f - size) * _scrollRect.verticalNormalizedPosition;
            var rect = _scrollIndicator.rectTransform;
            rect.anchorMin = new Vector2(0f, start);
            rect.anchorMax = new Vector2(1f, start + size);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _scrollIndicator.gameObject.SetActive(size < 0.995f);
        }
    }
}
