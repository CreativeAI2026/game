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

        public bool IsOpen => _isOpen;
        public int EntryCount => _entries.Count;

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
                SetOpen(!IsOpen);
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
            if (_panel != null || transform is not RectTransform root)
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
                _scrollRect.onValueChanged.AddListener(_ => RefreshLatestButton());
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
                _panel.SetActive(false);
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
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
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
        }
    }
}
