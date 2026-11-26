using System;
using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UIEventTrigger = UnityEngine.EventSystems.EventTrigger;

namespace CreativeAI.UI
{
    /// <summary>会話の選択肢。ボタンの生成・配置・フォーカス・登場と確定の演出を行い、選ばれるまで待つ。</summary>
    public sealed class DialogueChoiceView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _container; // 選択肢ボタンの親(通常は非表示)

        [SerializeField]
        private Button _buttonTemplate; // 選択肢ボタンの雛形(非active。実行時に複製)

        [SerializeField]
        private float _containerWidth = 565f; // 正式な選択肢画像の横幅

        [SerializeField]
        private float _buttonHeight = 70f; // 正式な選択肢画像の高さ

        [SerializeField]
        private float _spacing = 38f; // 選択肢同士の間隔

        [SerializeField]
        private float _bottomMargin = 70f; // 3択時の会話ウィンドウ上端から選択肢までの余白

        [SerializeField]
        private float _staggerDelay = 0.06f;

        [SerializeField]
        private float _enterDuration = 0.16f;

        [SerializeField]
        private float _confirmDuration = 0.2f;

        private readonly List<GameObject> _spawned = new();
        private readonly List<float> _spawnedHeights = new();
        private readonly Dictionary<Button, Color> _baseColors = new();
        private int _selectedIndex = -1;

        private const float FocusScale = 1.055f;
        private static readonly Color FocusTint = new(0.82f, 0.92f, 1f, 1f);

        private void OnDisable() => Clear();

        /// <summary>
        /// 選択肢を出して、選ばれるまで待つ。選ばれたら確定演出のあと片付けて、値と表示文を <paramref name="onPicked"/> で返す。
        /// 出せる選択肢が1つもなければ、<paramref name="onPicked"/> を呼ばずに終わる。
        /// </summary>
        /// <param name="selectedValues">以前に選んだ値。該当する選択肢は暗く表示する。</param>
        public IEnumerator Choose(
            IReadOnlyList<ChoiceOption> options,
            ISet<string> selectedValues,
            Action<string, string> onPicked
        )
        {
            bool hasPicked = false;
            string pickedValue = null;
            string pickedText = null;
            Button pickedButton = null;
            int count = Spawn(
                options,
                selectedValues,
                (value, text, button) =>
                {
                    hasPicked = true;
                    pickedValue = value;
                    pickedText = text;
                    pickedButton = button;
                }
            );
            if (count == 0)
            {
                Debug.LogWarning("[DialogueChoiceView] 表示できる選択肢がありません。");
                SetActive(false);
                yield break;
            }

            yield return AnimateIn();
            SelectFirst();
            while (!hasPicked)
                yield return null;

            yield return AnimateSelection(pickedButton);
            Clear();
            SetActive(false);
            onPicked?.Invoke(pickedValue, pickedText);
        }

        private int Spawn(
            IReadOnlyList<ChoiceOption> options,
            ISet<string> selectedValues,
            Action<string, string, Button> picked
        )
        {
            Clear();
            if (options == null || _buttonTemplate == null || _container == null)
                return 0;
            // The template must never become visible when its parent container is opened.
            _buttonTemplate.gameObject.SetActive(false);
            SetActive(true);

            foreach (var option in options)
            {
                if (option == null)
                    continue;

                var button = Instantiate(_buttonTemplate, _container);
                button.gameObject.SetActive(true);
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                if (!button.TryGetComponent(out CanvasGroup group))
                    group = button.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = option.Text;
                    label.textWrappingMode = TextWrappingModes.Normal;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = Mathf.Min(label.fontSize, 23f);
                    label.fontSizeMax = label.fontSize;
                }
                float buttonHeight = EstimateButtonHeight(option.Text);
                if (button.TryGetComponent(out LayoutElement layout))
                {
                    layout.minHeight = buttonHeight;
                    layout.preferredHeight = buttonHeight;
                }

                string value = option.Value;
                string text = option.Text;
                button.onClick.AddListener(() => picked(value, text, button));
                RegisterFocusFeedback(button);
                if (selectedValues != null && selectedValues.Contains(value))
                    SetPreviouslySelected(button, label);
                _spawned.Add(button.gameObject);
                _spawnedHeights.Add(buttonHeight);
            }

            UpdateLayout(_spawned.Count);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_container);
            return _spawned.Count;
        }

        /// <summary>選択肢の置き場ごと表示・非表示を切り替える。</summary>
        public void SetActive(bool active)
        {
            if (_container != null)
                _container.gameObject.SetActive(active);
        }

        /// <summary>出している選択肢ボタンを片付ける。</summary>
        public void Clear()
        {
            foreach (var choice in _spawned)
                if (choice != null)
                {
                    choice.SetActive(false);
                    Destroy(choice);
                }
            _spawned.Clear();
            _spawnedHeights.Clear();
            _baseColors.Clear();
            _selectedIndex = -1;
        }

        private void SelectFirst()
        {
            if (_spawned.Count == 0 || EventSystem.current == null)
                return;
            var button = _spawned[0].GetComponent<Button>();
            if (button != null && button.IsInteractable())
                Select(0);
        }

        /// <summary>キー・パッドでの選択肢の移動と決定を受け付ける。毎フレーム呼ぶ。</summary>
        public void HandleKeyboardInput()
        {
            if (_spawned.Count == 0)
                return;

            if (UIInputActionMap.TryReadDiscreteMove(out Vector2Int step))
            {
                if (step.y > 0)
                    SelectRelative(-1);
                else if (step.y < 0)
                    SelectRelative(1);
            }

            if (UIInputActionMap.WasSubmitPressedThisFrame())
            {
                if (_selectedIndex < 0)
                    Select(0);
                var button = ButtonAt(_selectedIndex);
                if (button != null && button.IsInteractable())
                    button.onClick.Invoke();
            }
        }

        private void UpdateLayout(int count)
        {
            if (_container == null || count <= 0)
                return;
            float height = 0f;
            for (int i = 0; i < _spawnedHeights.Count; i++)
                height += _spawnedHeights[i];
            height += (count - 1) * _spacing;
            if (_container.TryGetComponent(out VerticalLayoutGroup layout))
                layout.spacing = _spacing;
            _container.anchorMin = _container.anchorMax = new Vector2(0.5f, 1f);
            _container.pivot = new Vector2(0.5f, 0f);
            float threeChoiceHeight = 3f * _buttonHeight + 2f * _spacing;
            float centeredBottom = _bottomMargin + (threeChoiceHeight - height) * 0.5f;
            _container.anchoredPosition = new Vector2(0f, Mathf.Max(20f, centeredBottom));
            _container.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _containerWidth);
            _container.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private float EstimateButtonHeight(string text)
        {
            if (string.IsNullOrEmpty(text))
                return _buttonHeight;
            // Japanese text has few spaces, so character count is a more stable preview heuristic.
            int visualLines = Mathf.Clamp(Mathf.CeilToInt(text.Length / 19f), 1, 2);
            return _buttonHeight + (visualLines - 1) * 34f;
        }

        private void RegisterFocusFeedback(Button button)
        {
            var image = button.targetGraphic as Graphic;
            if (image != null)
                _baseColors[button] = image.color;

            if (!button.TryGetComponent(out UIEventTrigger trigger))
                trigger = button.gameObject.AddComponent<UIEventTrigger>();
            trigger.triggers ??= new List<UIEventTrigger.Entry>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, _ => Select(ButtonIndex(button)));
            AddTrigger(trigger, EventTriggerType.Select, _ => SetFocused(button, true));
            AddTrigger(trigger, EventTriggerType.Deselect, _ => SetFocused(button, false));
        }

        private static void AddTrigger(
            UIEventTrigger trigger,
            EventTriggerType type,
            UnityEngine.Events.UnityAction<BaseEventData> callback
        )
        {
            var entry = new UIEventTrigger.Entry { eventID = type };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }

        private void SelectRelative(int direction)
        {
            int start = _selectedIndex >= 0 ? _selectedIndex : 0;
            for (int offset = 1; offset <= _spawned.Count; offset++)
            {
                int index = (start + direction * offset + _spawned.Count) % _spawned.Count;
                var button = ButtonAt(index);
                if (button != null && button.IsInteractable())
                {
                    Select(index);
                    return;
                }
            }
        }

        private void Select(int index)
        {
            var button = ButtonAt(index);
            if (button == null || !button.IsInteractable())
                return;
            _selectedIndex = index;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
            SetFocused(button, true);
        }

        private void SetFocused(Button button, bool focused)
        {
            if (button == null)
                return;
            button.transform.localScale = Vector3.one * (focused ? FocusScale : 1f);
            if (
                button.targetGraphic is Graphic graphic
                && _baseColors.TryGetValue(button, out var color)
            )
                graphic.color = focused ? color * FocusTint : color;
        }

        private void SetPreviouslySelected(Button button, TMP_Text label)
        {
            if (button.targetGraphic is Graphic graphic)
            {
                Color selectedColor = new(
                    graphic.color.r * 0.5f,
                    graphic.color.g * 0.5f,
                    graphic.color.b * 0.5f,
                    graphic.color.a
                );
                graphic.color = selectedColor;
                _baseColors[button] = selectedColor;
            }
            if (label != null)
            {
                Color color = label.color;
                label.color = new Color(color.r * 0.72f, color.g * 0.72f, color.b * 0.72f, 0.78f);
            }
        }

        private int ButtonIndex(Button button)
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null && _spawned[i].GetComponent<Button>() == button)
                    return i;
            return -1;
        }

        private Button ButtonAt(int index) =>
            index >= 0 && index < _spawned.Count && _spawned[index] != null
                ? _spawned[index].GetComponent<Button>()
                : null;

        private static float FrameDelta() => Mathf.Max(Time.unscaledDeltaTime, 1f / 60f);

        private IEnumerator AnimateIn()
        {
            foreach (var choice in _spawned)
            {
                if (choice == null)
                    continue;
                var group = choice.GetComponent<CanvasGroup>();
                var rect = choice.transform as RectTransform;
                Vector2 target = rect != null ? rect.anchoredPosition : Vector2.zero;
                Vector2 start = target + Vector2.up * 18f;
                float duration = Mathf.Max(0.01f, _enterDuration);
                for (float elapsed = 0f; elapsed < duration; elapsed += FrameDelta())
                {
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    if (group != null)
                        group.alpha = t;
                    if (rect != null)
                        rect.anchoredPosition = Vector2.Lerp(start, target, t);
                    yield return null;
                }
                if (group != null)
                    group.alpha = 1f;
                if (rect != null)
                    rect.anchoredPosition = target;
                if (_staggerDelay > 0f)
                    yield return new WaitForSecondsRealtime(_staggerDelay);
            }
        }

        private IEnumerator AnimateSelection(Button selected)
        {
            foreach (var choice in _spawned)
            {
                var button = choice != null ? choice.GetComponent<Button>() : null;
                if (button != null)
                    button.interactable = false;
            }

            float duration = Mathf.Max(0.01f, _confirmDuration);
            for (float elapsed = 0f; elapsed < duration; elapsed += FrameDelta())
            {
                float t = elapsed / duration;
                foreach (var choice in _spawned)
                {
                    if (choice == null)
                        continue;
                    var group = choice.GetComponent<CanvasGroup>();
                    var button = choice.GetComponent<Button>();
                    if (group != null)
                        group.alpha = button == selected ? 1f : Mathf.Lerp(1f, 0.25f, t);
                    if (button == selected)
                        choice.transform.localScale =
                            Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.04f);
                }
                yield return null;
            }
        }
    }
}
