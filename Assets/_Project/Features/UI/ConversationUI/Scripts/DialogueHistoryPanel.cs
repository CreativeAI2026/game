using System;
using System.Collections.Generic;
using System.Text;
using CreativeAI.Core.EventSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>会話中に表示済みの行を読み返す、画面全体の履歴パネル。</summary>
    public sealed class DialogueHistoryPanel : MonoBehaviour
    {
        [SerializeField]
        private int _maxEntries = 200;

        [SerializeField]
        [Range(0f, 1f)]
        private float _backdropAlpha = 0.78f;

        private readonly List<DialogueHistoryEntry> _entries = new();
        private DialogueHistoryViewFactory _viewFactory;

        [SerializeField]
        private TMP_FontAsset _font;

        [SerializeField]
        private GameObject _panel;

        [SerializeField]
        private GameObject _openButton;

        [SerializeField]
        private RectTransform _content;

        [SerializeField]
        private ScrollRect _scrollRect;

        [SerializeField]
        private CanvasGroup _panelGroup;

        [SerializeField]
        private GameObject _latestButton;
        private Coroutine _panelAnimation;
        private bool _isOpen;

        [SerializeField]
        private TMP_InputField _searchField;

        [SerializeField]
        private Image _scrollIndicator;

        public bool IsOpen => _isOpen;
        public int EntryCount => _entries.Count;
        public event Action Closed;

        public void Initialize(TMP_FontAsset font)
        {
            if (_font == null)
                _font = font;
            EnsureView();
            BindViewEvents();
        }

#if UNITY_EDITOR
        public void BuildPrefabView(TMP_FontAsset font)
        {
            _font = font;
            EnsureView();
        }
#endif

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.dKey.wasPressedThisFrame)
            {
                if (
                    _openButton != null
                    && _openButton.TryGetComponent<ConversationControlButton>(out var control)
                )
                    control.PlayShortcutFeedback();
                SetOpen(!IsOpen);
            }
            else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame)
                SetOpen(false);
        }

        public void AddEntry(
            string speaker,
            string body,
            Sprite portrait,
            DialoguePortraitSide side,
            bool portraitObscured = false
        )
        {
            bool followLatest = ShouldFollowLatest();
            EnsureView();
            var entry = new DialogueHistoryEntry(
                speaker ?? string.Empty,
                body ?? string.Empty,
                portrait,
                side,
                portraitObscured,
                _entries.Count + 1
            );
            _entries.Add(entry);
            _viewFactory.CreateEntry(_content, entry);
            TrimOldEntries();
            RefreshCurrentMarker();
            if (followLatest)
                ScrollToLatest();
            else
                RefreshLatestButton();
        }

        public void AddChoiceEntry(string choiceText)
        {
            if (string.IsNullOrWhiteSpace(choiceText))
                return;

            bool followLatest = ShouldFollowLatest();
            EnsureView();
            var entry = new DialogueHistoryEntry(choiceText, _entries.Count + 1);
            _entries.Add(entry);
            _viewFactory.CreateEntry(_content, entry);
            TrimOldEntries();
            RefreshCurrentMarker();
            if (followLatest)
                ScrollToLatest();
            else
                RefreshLatestButton();
        }

        public void AddRewardEntry(string rewardText, bool weapon)
        {
            if (string.IsNullOrWhiteSpace(rewardText))
                return;
            bool followLatest = ShouldFollowLatest();
            EnsureView();
            var entry = new DialogueHistoryEntry(rewardText, weapon, _entries.Count + 1);
            _entries.Add(entry);
            _viewFactory.CreateEntry(_content, entry);
            TrimOldEntries();
            RefreshCurrentMarker();
            if (followLatest)
                ScrollToLatest();
            else
                RefreshLatestButton();
        }

        public void AddChoiceEntry(IReadOnlyList<ChoiceOption> options, string selectedText)
        {
            if (options == null || options.Count == 0)
            {
                AddChoiceEntry(selectedText);
                return;
            }

            var summary = new StringBuilder();
            foreach (var option in options)
            {
                if (option == null)
                    continue;
                if (summary.Length > 0)
                    summary.AppendLine();
                summary.Append(option.Text == selectedText ? "✓ " : "・ ").Append(option.Text);
            }
            AddChoiceEntry(summary.ToString());
        }

        public void SetOpen(bool open)
        {
            EnsureView();
            if (_panel == null)
                return;

            _isOpen = open;
            if (_openButton != null)
                _openButton.SetActive(!open);

            if (open)
            {
                bool wasActive = _panel.activeSelf;
                _panel.SetActive(true);
                if (!wasActive && _panelGroup != null)
                    _panelGroup.alpha = 0f;
                _panel.transform.SetAsLastSibling();
                ScrollToLatest();
                EventSystem.current?.SetSelectedGameObject(null);
            }

            if (!Application.isPlaying)
            {
                _panel.SetActive(open);
                if (_panelGroup != null)
                    _panelGroup.alpha = open ? 1f : 0f;
                return;
            }

            if (_panelAnimation != null)
                StopCoroutine(_panelAnimation);
            _panelAnimation = StartCoroutine(AnimatePanel(open));
        }

        private void EnsureView()
        {
            _viewFactory ??= new DialogueHistoryViewFactory(_font);
            if (_panel != null)
            {
                ApplyLayoutPolish();
                return;
            }
            if (transform is not RectTransform root)
                return;

            var view = _viewFactory.Build(
                root,
                _backdropAlpha,
                () => SetOpen(true),
                () => SetOpen(false),
                ScrollToLatest,
                ApplyFilter,
                _ => RefreshLatestButton()
            );
            _panel = view.Panel;
            _openButton = view.OpenButton;
            _content = view.Content;
            _scrollRect = view.ScrollRect;
            _panelGroup = view.PanelGroup;
            _latestButton = view.LatestButton;
            _searchField = view.SearchField;
            _scrollIndicator = view.ScrollIndicator;
            ApplyLayoutPolish();
        }

        private void ApplyLayoutPolish()
        {
            if (_searchField != null)
            {
                AlignSearchText(_searchField.textComponent);
                AlignSearchText(_searchField.placeholder as TMP_Text);
            }

            CenterButtonLabel(
                _panel != null
                    ? _panel.transform.Find("ArchiveHeader/CloseButton")?.gameObject
                    : null
            );
            CenterButtonLabel(_latestButton);
        }

        private static void AlignSearchText(TMP_Text text)
        {
            if (text == null)
                return;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.margin = new Vector4(14f, 0f, 14f, 0f);
        }

        private static void CenterButtonLabel(GameObject button)
        {
            var label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (label == null)
                return;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
        }

        private void BindViewEvents()
        {
            if (_openButton != null && _openButton.TryGetComponent<Button>(out var openButton))
            {
                openButton.onClick.RemoveAllListeners();
                openButton.onClick.AddListener(() => SetOpen(true));
            }
            if (
                _latestButton != null
                && _latestButton.TryGetComponent<Button>(out var latestButton)
            )
            {
                latestButton.onClick.RemoveAllListeners();
                latestButton.onClick.AddListener(ScrollToLatest);
            }
            var closeTransform =
                _panel != null ? _panel.transform.Find("ArchiveHeader/CloseButton") : null;
            if (
                closeTransform != null
                && closeTransform.TryGetComponent<Button>(out var closeButton)
            )
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() => SetOpen(false));
            }
            if (_scrollRect != null)
            {
                _scrollRect.onValueChanged.RemoveAllListeners();
                _scrollRect.onValueChanged.AddListener(_ =>
                {
                    RefreshLatestButton();
                    RefreshScrollIndicator();
                });
            }
            if (_searchField != null)
            {
                _searchField.onValueChanged.RemoveAllListeners();
                _searchField.onValueChanged.AddListener(ApplyFilter);
            }
        }

        private System.Collections.IEnumerator AnimatePanel(bool opening)
        {
            float start = _panelGroup != null ? _panelGroup.alpha : (opening ? 0f : 1f);
            float target = opening ? 1f : 0f;
            float elapsed = 0f;
            const float duration = 0.18f;
            while (elapsed < duration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                if (_panelGroup != null)
                    _panelGroup.alpha = Mathf.Lerp(start, target, t);
                elapsed += Mathf.Max(Time.unscaledDeltaTime, 1f / 60f);
                yield return null;
            }
            if (_panelGroup != null)
                _panelGroup.alpha = target;
            if (!opening)
            {
                _panel.SetActive(false);
                Closed?.Invoke();
            }
            _panelAnimation = null;
        }

        private void RefreshLatestButton()
        {
            if (_latestButton != null)
                _latestButton.SetActive(IsOpen && _scrollRect.verticalNormalizedPosition > 0.03f);
        }

        private bool ShouldFollowLatest() =>
            !IsOpen || _scrollRect == null || _scrollRect.verticalNormalizedPosition <= 0.03f;

        public void ApplyFilter(string query)
        {
            query = query?.Trim() ?? string.Empty;
            for (int i = 0; i < _entries.Count && i < _content.childCount; i++)
            {
                var entry = _entries[i];
                bool visible =
                    query.Length == 0
                    || entry.Speaker.Contains(query, System.StringComparison.OrdinalIgnoreCase)
                    || entry.Body.Contains(query, System.StringComparison.OrdinalIgnoreCase);
                _content.GetChild(i).gameObject.SetActive(visible);
                ApplyEntryHighlight(_content.GetChild(i), entry, query);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            RefreshScrollIndicator();
        }

        private static void ApplyEntryHighlight(
            Transform container,
            DialogueHistoryEntry entry,
            string query
        )
        {
            var choiceToggle = container.GetComponentInChildren<DialogueChoiceHistoryToggle>(true);
            if (choiceToggle != null)
            {
                choiceToggle.SetSearchText(
                    string.IsNullOrEmpty(query) ? null : Highlight(entry.Body, query)
                );
                return;
            }
            foreach (var text in container.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Name")
                    text.text = Highlight(entry.Speaker, query);
                else if (text.name is "Body" or "ChoiceText")
                    text.text = Highlight(entry.Body, query);
            }
        }

        private static string Highlight(string source, string query)
        {
            source ??= string.Empty;
            if (string.IsNullOrEmpty(query))
                return source;
            int index = source.IndexOf(query, System.StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return source;
            return source.Substring(0, index)
                + "<mark=#3A84A880>"
                + source.Substring(index, query.Length)
                + "</mark>"
                + source.Substring(index + query.Length);
        }

        private void RefreshCurrentMarker()
        {
            if (_content == null)
                return;
            for (int i = 0; i < _content.childCount; i++)
            {
                var marker = _content.GetChild(i).Find("CurrentMarker");
                if (marker != null)
                    marker.gameObject.SetActive(i == _content.childCount - 1);
            }
        }

        private void TrimOldEntries()
        {
            while (_entries.Count > Mathf.Max(1, _maxEntries))
            {
                _entries.RemoveAt(0);
                if (_content != null && _content.childCount > 0)
                    Destroy(_content.GetChild(0).gameObject);
            }
        }

        private void ScrollToLatest()
        {
            if (_scrollRect == null)
                return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _scrollRect.verticalNormalizedPosition = 0f;
            RefreshLatestButton();
            RefreshScrollIndicator();
        }

        private void RefreshScrollIndicator()
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
