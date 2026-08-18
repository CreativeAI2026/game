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
        private readonly DialogueHistoryScrollController _scrollController = new();
        private DialogueHistoryPanelAnimator _panelAnimator;
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
            AppendEntry(
                new DialogueHistoryEntry(
                    speaker ?? string.Empty,
                    body ?? string.Empty,
                    portrait,
                    side,
                    portraitObscured,
                    _entries.Count + 1
                )
            );
        }

        public void AddChoiceEntry(string choiceText)
        {
            if (string.IsNullOrWhiteSpace(choiceText))
                return;

            AppendEntry(new DialogueHistoryEntry(choiceText, _entries.Count + 1));
        }

        public void AddRewardEntry(string rewardText, bool weapon)
        {
            if (string.IsNullOrWhiteSpace(rewardText))
                return;
            AppendEntry(new DialogueHistoryEntry(rewardText, weapon, _entries.Count + 1));
        }

        private void AppendEntry(DialogueHistoryEntry entry)
        {
            bool followLatest = _scrollController.ShouldFollowLatest();
            EnsureView();
            _entries.Add(entry);
            _viewFactory.CreateEntry(_content, entry);
            TrimOldEntries();
            RefreshCurrentMarker();
            if (followLatest)
                _scrollController.ScrollToLatest();
            else
                _scrollController.RefreshLatestButton();
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
                _scrollController.ScrollToLatest();
                EventSystem.current?.SetSelectedGameObject(null);
            }

            if (!Application.isPlaying)
            {
                _panel.SetActive(open);
                if (_panelGroup != null)
                    _panelGroup.alpha = open ? 1f : 0f;
                return;
            }

            _panelAnimator.Play(open);
        }

        private void EnsureView()
        {
            _viewFactory ??= new DialogueHistoryViewFactory(_font);
            if (_panel != null)
            {
                ConfigurePanelAnimator();
                ConfigureScrollController();
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
                _scrollController.ScrollToLatest,
                ApplyFilter,
                _ => _scrollController.RefreshLatestButton()
            );
            _panel = view.Panel;
            _openButton = view.OpenButton;
            _content = view.Content;
            _scrollRect = view.ScrollRect;
            _panelGroup = view.PanelGroup;
            _latestButton = view.LatestButton;
            _searchField = view.SearchField;
            _scrollIndicator = view.ScrollIndicator;
            ConfigurePanelAnimator();
            ConfigureScrollController();
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
                latestButton.onClick.AddListener(_scrollController.ScrollToLatest);
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
                    _scrollController.Refresh();
                });
            }
            if (_searchField != null)
            {
                _searchField.onValueChanged.RemoveAllListeners();
                _searchField.onValueChanged.AddListener(ApplyFilter);
            }
        }

        public void ApplyFilter(string query)
        {
            query = query?.Trim() ?? string.Empty;
            for (int i = 0; i < _entries.Count && i < _content.childCount; i++)
            {
                var entry = _entries[i];
                bool visible = DialogueHistorySearch.Matches(entry, query);
                _content.GetChild(i).gameObject.SetActive(visible);
                ApplyEntryHighlight(_content.GetChild(i), entry, query);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _scrollController.Refresh();
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
                    string.IsNullOrEmpty(query)
                        ? null
                        : DialogueHistorySearch.Highlight(entry.Body, query)
                );
                return;
            }
            foreach (var text in container.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Name")
                    text.text = DialogueHistorySearch.Highlight(entry.Speaker, query);
                else if (text.name is "Body" or "ChoiceText")
                    text.text = DialogueHistorySearch.Highlight(entry.Body, query);
            }
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
                {
                    var oldest = _content.GetChild(0).gameObject;
                    if (Application.isPlaying)
                        Destroy(oldest);
                    else
                        DestroyImmediate(oldest);
                }
            }
        }

        private void ConfigureScrollController()
        {
            _scrollController.Configure(
                _scrollRect,
                _content,
                _latestButton,
                _scrollIndicator,
                () => IsOpen
            );
        }

        private void ConfigurePanelAnimator()
        {
            _panelAnimator ??= new DialogueHistoryPanelAnimator(this);
            _panelAnimator.Configure(_panel, _panelGroup, () => Closed?.Invoke());
        }
    }
}
