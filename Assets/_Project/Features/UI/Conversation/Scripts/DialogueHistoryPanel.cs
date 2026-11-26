using System;
using System.Collections.Generic;
using System.Text;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>会話中に表示済みの行を読み返す、画面全体の履歴パネル。</summary>
    public sealed class DialogueHistoryPanel : MonoBehaviour
    {
        [SerializeField]
        private int _maxEntries = 200;

        [Header("行の雛形(種類ごと)")]
        [SerializeField]
        private DialogueHistoryRow _dialogueRow;

        [SerializeField]
        private DialogueHistoryRow _choiceRow;

        [SerializeField]
        private DialogueHistoryRow _narrationRow;

        [SerializeField]
        private DialogueHistoryRow _rewardRow;

        [Header("パネル")]
        [SerializeField]
        private GameObject _panel;

        [SerializeField]
        private Button _closeButton;

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

        private readonly List<DialogueHistoryEntry> _entries = new();
        private readonly List<DialogueHistoryRow> _rows = new(); // _entries と同じ並び
        private readonly DialogueHistoryScrollController _scrollController = new();
        private DialogueHistoryPanelAnimator _panelAnimator;

        public bool IsOpen => _isOpen;
        public int EntryCount => _entries.Count;
        public event Action Closed;

        /// <summary>ボタンやスクロールの通知をつなぐ。会話UIの起動時に1回呼ぶ。</summary>
        public void Initialize()
        {
            EnsureView();
            BindViewEvents();
        }

        private void Update()
        {
            if (UIInputActionMap.WasPressedThisFrame(UIInputActionMap.DialogueHistory))
            {
                if (
                    _openButton != null
                    && _openButton.TryGetComponent<ConversationControlButton>(out var control)
                )
                    control.PlayShortcutFeedback();
                SetOpen(!IsOpen);
            }
            else if (IsOpen && UIInputActionMap.WasCancelPressedThisFrame())
            {
                SetOpen(false);
            }

            if (IsOpen && _scrollRect != null)
            {
                float scrollInput = UIInputActionMap.ReadNavigate().y;
                if (Mathf.Abs(scrollInput) >= 0.2f)
                {
                    float contentHeight = _content != null ? _content.rect.height : 0f;
                    float viewportHeight =
                        _scrollRect.viewport != null ? _scrollRect.viewport.rect.height : 0f;
                    float scrollable = Mathf.Max(1f, contentHeight - viewportHeight);
                    float delta = (scrollInput * 600f * Time.unscaledDeltaTime) / scrollable;
                    _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                        _scrollRect.verticalNormalizedPosition + delta
                    );
                }
            }
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
            var row = Instantiate(RowTemplate(entry.Kind), _content);
            row.Set(entry);
            _rows.Add(row);
            TrimOldEntries();
            RefreshCurrentMarker();
            // 閉じている間はスクロールしない(全 Canvas の強制更新が重い)。開くときに SetOpen が最新へ送る
            if (!_isOpen)
                return;
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
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
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

        private DialogueHistoryRow RowTemplate(DialogueHistoryEntryKind kind) =>
            kind switch
            {
                DialogueHistoryEntryKind.Dialogue => _dialogueRow,
                DialogueHistoryEntryKind.Choice => _choiceRow,
                DialogueHistoryEntryKind.Narration => _narrationRow,
                _ => _rewardRow,
            };

        private void EnsureView()
        {
            ConfigurePanelAnimator();
            ConfigureScrollController();
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
            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(() => SetOpen(false));
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
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                _rows[i].gameObject.SetActive(DialogueHistorySearch.Matches(entry, query));
                _rows[i].ApplySearch(entry, query);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _scrollController.Refresh();
        }

        private void RefreshCurrentMarker()
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].SetCurrent(i == _rows.Count - 1);
        }

        private void TrimOldEntries()
        {
            while (_entries.Count > Mathf.Max(1, _maxEntries))
            {
                _entries.RemoveAt(0);
                var oldest = _rows[0].gameObject;
                _rows.RemoveAt(0);
                if (Application.isPlaying)
                    Destroy(oldest);
                else
                    DestroyImmediate(oldest);
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

    /// <summary>履歴検索の一致判定と表示用ハイライトを担当する。</summary>
    internal static class DialogueHistorySearch
    {
        private const string MarkupPrefix = "<mark=#3A84A880>";
        private const string MarkupSuffix = "</mark>";

        public static bool Matches(DialogueHistoryEntry entry, string query) =>
            string.IsNullOrEmpty(query)
            || Contains(entry.Speaker, query)
            || Contains(entry.Body, query);

        public static string Highlight(string source, string query)
        {
            source ??= string.Empty;
            if (string.IsNullOrEmpty(query))
                return source;

            int index = source.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return source;

            return source.Substring(0, index)
                + MarkupPrefix
                + source.Substring(index, query.Length)
                + MarkupSuffix
                + source.Substring(index + query.Length);
        }

        private static bool Contains(string source, string query) =>
            source?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
