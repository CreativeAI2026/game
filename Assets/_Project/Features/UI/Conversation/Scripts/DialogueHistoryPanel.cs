using System;
using System.Collections;
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
        private Coroutine _fade;

        public bool IsOpen => _isOpen;
        public int EntryCount => _entries.Count;
        public event Action Closed;

        /// <summary>ボタンやスクロールの通知をつなぐ。会話UIの起動時に1回呼ぶ。</summary>
        public void Initialize()
        {
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
            // 最新の行を見ている(か閉じている)なら、足した行へ送る
            bool followLatest = !_isOpen || _scrollRect == null || !IsScrolledUp();
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

            if (_fade != null)
                StopCoroutine(_fade);
            _fade = StartCoroutine(Fade(open));
        }

        // 開閉のフェード。閉じ終えたらパネルを隠して Closed を出す
        private IEnumerator Fade(bool opening)
        {
            float start = _panelGroup != null ? _panelGroup.alpha : (opening ? 0f : 1f);
            float target = opening ? 1f : 0f;
            const float duration = 0.18f;
            for (float elapsed = 0f; elapsed < duration; )
            {
                if (_panelGroup != null)
                    _panelGroup.alpha = Mathf.Lerp(
                        start,
                        target,
                        Mathf.SmoothStep(0f, 1f, elapsed / duration)
                    );
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
            _fade = null;
        }

        private DialogueHistoryRow RowTemplate(DialogueHistoryEntryKind kind) =>
            kind switch
            {
                DialogueHistoryEntryKind.Dialogue => _dialogueRow,
                DialogueHistoryEntryKind.Choice => _choiceRow,
                DialogueHistoryEntryKind.Narration => _narrationRow,
                _ => _rewardRow,
            };

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
            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(() => SetOpen(false));
            if (_scrollRect != null)
            {
                _scrollRect.onValueChanged.RemoveAllListeners();
                _scrollRect.onValueChanged.AddListener(_ => RefreshScrollMarkers());
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
            RefreshScrollMarkers();
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

        // --- スクロール(最新へ送る・「最新へ」ボタン・スクロール位置の目印) ---

        private bool IsScrolledUp() => _scrollRect.verticalNormalizedPosition > 0.03f;

        private void ScrollToLatest()
        {
            if (_scrollRect == null || _content == null)
                return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _scrollRect.verticalNormalizedPosition = 0f;
            RefreshScrollMarkers();
        }

        private void RefreshScrollMarkers()
        {
            RefreshLatestButton();
            RefreshScrollIndicator();
        }

        // 上へ遡っている間だけ「最新へ」ボタンを出す
        private void RefreshLatestButton()
        {
            if (_latestButton == null || _scrollRect == null)
                return;
            _latestButton.SetActive(_isOpen && IsScrolledUp());
        }

        // 右端の目印を、見えている範囲の位置と大きさに合わせる
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
