using System;
using System.Collections;
using System.Collections.Generic;
using CreativeAI.Core.EventSystem;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>
    /// 会話UIの実体。<see cref="IDialogueView"/> を実装し、生成時に <see cref="DialogueViewService.Current"/>
    /// へ自身を登録する(EventPlayer は常駐生成で drag 配線できないため seam 経由で受け取る、IDialogueView 参照)。
    /// 仕様§6のとおり <see cref="UIRoot"/> が束ねる UI レイヤーの一部で、UIRoot Prefab の子として同梱される
    /// (常駐・単一化・DontDestroyOnLoad は UIRoot が担うため、このコンポーネント自身は自己生成も DDOL もしない)。状態は保存しない。
    /// 会話中でないときはウィンドウを隠す(再生時 Awake で alpha=0)。編集時は Awake が走らないため
    /// Prefab の見た目(立ち絵+ウィンドウ+ダミー文)がそのままプレビューになる。
    /// documents/Specification.md「常駐アーキテクチャ」/ UIImplementation.md 参照。
    /// </summary>
    public sealed class ConversationView : MonoBehaviour, IDialogueView
    {
        public static ConversationView Instance { get; private set; }
        public event Action<string, string> ExternalPresentationCommandRequested;

        public enum ConversationState
        {
            Hidden,
            Entering,
            Typing,
            WaitingForAdvance,
            ShowingChoices,
            Exiting,
        }

        public enum TextSpeed
        {
            Slow,
            Normal,
            Fast,
            Instant,
        }

        public enum PortraitEffect
        {
            Shake,
            Jump,
            Fade,
        }

        public ConversationState State { get; private set; } = ConversationState.Hidden;
        public bool IsAutoMode { get; private set; }

        /// <summary>portrait キー → 立ち絵スプライトの対応。未登録キーは <see cref="_defaultPortrait"/> にフォールバック。</summary>
        [Serializable]
        public struct PortraitEntry
        {
            public string Key;
            public Sprite Sprite;
            public DialoguePortraitSide Side;
        }

        [Header("ルート表示")]
        [SerializeField]
        private CanvasGroup _root; // ウィンドウ全体の表示/非表示。非会話時は alpha=0

        [SerializeField]
        private RectTransform _windowRoot;

        [Header("表示要素")]
        [SerializeField]
        private Image _portrait; // 立ち絵

        [SerializeField]
        private Image _rightPortrait; // 実行時に _portrait から生成する右側スロット

        [SerializeField]
        private float _portraitLeftAnchorX = 0.12f; // 主人公など画面左側に立つキャラクター

        [SerializeField]
        private float _portraitRightAnchorX = 0.88f; // 会話相手など画面右側に立つキャラクター

        [SerializeField]
        private float _portraitActiveScale = 1.03f;

        [SerializeField]
        private float _portraitInactiveScale = 0.92f;

        [SerializeField]
        [Range(0f, 1f)]
        private float _portraitInactiveBrightness = 0.45f;

        [SerializeField]
        private float _portraitFadeDuration = 0.3f;

        [SerializeField]
        private float _portraitFocusDuration = 0.2f;

        [SerializeField]
        private TMP_Text _nameText; // 名前プレート

        [SerializeField]
        private TMP_Text _bodyText; // 本文

        [SerializeField]
        private GameObject _nextIndicator; // 送り待ちに小さくバウンドするインジケーター

        [Header("選択肢")]
        [SerializeField]
        private RectTransform _choiceContainer; // 選択肢ボタンの親(通常は非表示)

        [SerializeField]
        private Button _choiceButtonTemplate; // 選択肢ボタンの雛形(非active。実行時に複製)

        [SerializeField]
        private float _choiceContainerWidth = 565f; // 正式な選択肢画像の横幅

        [SerializeField]
        private float _choiceButtonHeight = 70f; // 正式な選択肢画像の高さ

        [SerializeField]
        private float _choiceSpacing = 38f; // 選択肢同士の間隔

        [SerializeField]
        private float _choiceBottomMargin = 70f; // 3択時の会話ウィンドウ上端から選択肢までの余白

        [SerializeField]
        private float _choiceStaggerDelay = 0.06f;

        [SerializeField]
        private float _choiceEnterDuration = 0.16f;

        [SerializeField]
        private float _choiceConfirmDuration = 0.2f;

        [Header("演出")]
        [SerializeField]
        private float _charInterval = 0.03f; // タイプライターの1文字あたり待ち時間(秒)

        [SerializeField]
        private float _punctuationDelay = 0.12f;

        [SerializeField]
        private TextSpeed _textSpeed = TextSpeed.Normal;

        [SerializeField]
        [Range(0.05f, 1f)]
        private float _fastForwardMultiplier = 0.2f;

        [SerializeField]
        private AudioSource _typingAudioSource;

        [SerializeField]
        private AudioClip _typingSound;

        [SerializeField]
        private float _windowEnterDuration = 0.2f;

        [SerializeField]
        private float _windowEnterOffsetY = -24f;

        [Header("オートモード")]
        [SerializeField]
        private TMP_Text _autoModeIndicator;

        [SerializeField]
        private TMP_Text _controlGuide;

        [SerializeField]
        private Image _autoProgressFill;

        [SerializeField]
        private float _autoAdvanceDelay = 1.2f;

        [SerializeField]
        private string _autoModeLabel = "AUTO";

        [SerializeField]
        private DialogueHistoryPanel _historyPanel;

        [SerializeField]
        private float _indicatorBounceHeight = 8f; // 基準位置から上へ動く距離(px)

        [SerializeField]
        private float _indicatorBounceDuration = 0.6f; // 上昇して基準位置へ戻るまでの時間(秒)

        [SerializeField]
        private Sprite _defaultPortrait; // portrait キー未指定/未登録時の立ち絵

        [SerializeField]
        private DialoguePortraitSide _defaultPortraitSide = DialoguePortraitSide.Left;

        [SerializeField]
        private DialogueCharacterDefinition[] _characters =
            Array.Empty<DialogueCharacterDefinition>();

        [SerializeField]
        private PortraitEntry[] _portraits = Array.Empty<PortraitEntry>();

        [Header("アイテム受け取り表示")]
        [SerializeField]
        private Sprite _itemGetSprite; // 受け取ったアイテムのダミー画像(将来は giveItem の itemKey から解決)

        [SerializeField]
        private Vector2 _itemGetSize = new(256f, 256f); // 表示サイズ

        [SerializeField]
        private Vector2 _itemGetPosition = new(0f, 220f); // Canvas 中央からのオフセット(ウィンドウの上あたり)

        [Header("武器モデル受け取り表示")]
        [SerializeField]
        private GameObject _weaponModelPrefab; // 武器のダミー3Dモデル(Katana)。将来は giveWeapon の weaponKey から解決

        [SerializeField]
        private Vector3 _weaponModelEuler = new(10f, -20f, 0f); // 静止表示するモデルの傾き(見栄えの3/4アングル)

        [SerializeField]
        private int _weaponTextureSize = 512; // RenderTexture の解像度

        [SerializeField]
        private Vector2 _weaponImageSize = new(640f, 360f); // 武器表示枠(横長。刀に合わせた比率。0なら _itemGetSize)

        [SerializeField]
        private float _weaponFrameFill = 1.3f; // 投影サイズへの余白率(1.0でぴったり。小さいほど大きく写る)

        [SerializeField]
        private Color _weaponBackdropColor = new(0f, 0f, 0f, 0.4f); // 武器背後の枠(横長。枠サイズに連動。a=0で消せる)

        [SerializeField]
        private Image _itemRewardImage;

        [SerializeField]
        private Image _itemRewardBackdrop;

        [SerializeField]
        private RawImage _weaponRewardImage;

        [SerializeField]
        private Image _weaponRewardBackdrop;

        [SerializeField]
        private Button _autoControlButton;

        [SerializeField]
        private Button _skipControlButton;

        [SerializeField]
        private Button _speedControlButton;

        [SerializeField]
        private TMP_Text _speedControlLabel;

        [SerializeField]
        private TMP_Text _speedToast;

        [SerializeField]
        private Button _hideControlButton;

        private DialogueChoicePresenter _choicePresenter;
        private readonly DialogueTextPlayer _textPlayer = new();
        private readonly DialoguePortraitPresenter _portraitPresenter = new();
        private ConversationChromePresenter _chromePresenter;
        private readonly DialogueAdvanceController _advanceController = new();
        private DialogueRewardPresenter _rewardPresenter;
        private Coroutine _speedToastRoutine;
        private bool _historyEventsBound;
        private bool _windowManuallyHidden;
        private readonly HashSet<string> _readLineIds = new();
        private bool _currentLineWasRead;
        private string _currentLineId;
        private AudioClip _currentTypingSound;
        private bool _skipMode;
        private bool _controlsBound;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // 二重生成ガード(冪等)。単一化は UIRoot が担うため通常起きない
                return;
            }
            Instance = this;

            DialogueViewService.Current = this; // EventPlayer が参照する seam へ自身を登録
            InitializePresenters();
            _portraitPresenter.EnsureSlots();
            _rightPortrait = _portraitPresenter.RightPortrait;
            _chromePresenter.SetAutoMode(IsAutoMode);
            UpdateSpeedControlLabel();
            BindControlButtons();
            EnsureHistoryPanel();
            HideImmediate(); // 会話開始まで隠す(編集時は Awake が走らずプレビューが見える)
        }

        private void InitializePresenters()
        {
            _chromePresenter ??= new ConversationChromePresenter(this);
            _chromePresenter.Configure(
                _root,
                _windowRoot,
                _nameText,
                _nextIndicator,
                _autoModeIndicator,
                _controlGuide,
                _autoProgressFill,
                _indicatorBounceHeight,
                _indicatorBounceDuration,
                _windowEnterDuration,
                _windowEnterOffsetY,
                _autoModeLabel
            );
            _choicePresenter = new DialogueChoicePresenter(
                _choiceContainer,
                _choiceButtonTemplate,
                _choiceContainerWidth,
                _choiceButtonHeight,
                _choiceSpacing,
                _choiceBottomMargin,
                _choiceStaggerDelay,
                _choiceEnterDuration,
                _choiceConfirmDuration
            );
            _rewardPresenter = new DialogueRewardPresenter(
                transform,
                _itemRewardImage,
                _itemRewardBackdrop,
                _weaponRewardImage,
                _weaponRewardBackdrop
            );
            _portraitPresenter.Configure(
                _portrait,
                _rightPortrait,
                _portraitLeftAnchorX,
                _portraitRightAnchorX,
                _portraitActiveScale,
                _portraitInactiveScale,
                _portraitInactiveBrightness,
                _portraitFadeDuration,
                _portraitFocusDuration
            );
            _chromePresenter.EnsureView();
            _autoModeIndicator = _chromePresenter.AutoIndicator;
            _controlGuide = _chromePresenter.ControlGuide;
            _autoProgressFill = _chromePresenter.AutoProgress;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.aKey.wasPressedThisFrame)
            {
                PlayControlShortcut(_autoControlButton);
                SetAutoMode(!IsAutoMode);
            }
            if (keyboard != null && keyboard.sKey.wasPressedThisFrame)
                PlayControlShortcut(_skipControlButton);
            if (
                keyboard != null
                && keyboard.hKey.wasPressedThisFrame
                && State != ConversationState.ShowingChoices
            )
            {
                PlayControlShortcut(_hideControlButton);
                SetWindowHidden(!_windowManuallyHidden);
            }
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
            {
                PlayControlShortcut(_speedControlButton);
                SetTextSpeed((TextSpeed)(((int)_textSpeed + 1) % 4));
            }

            if (State == ConversationState.ShowingChoices)
                _choicePresenter?.HandleKeyboardInput();

            _chromePresenter?.Tick(IsAutoMode, _advanceController.Progress);
        }

        private void OnDestroy()
        {
            _rewardPresenter?.HideAll();
            if (Instance == this)
                Instance = null;
            if (ReferenceEquals(DialogueViewService.Current, this))
                DialogueViewService.Current = null;
        }

        private void OnDisable()
        {
            _rewardPresenter?.HideAll();
            _choicePresenter?.Clear();
        }

        /// <summary>1行を立ち絵付きで表示し、タイプライター送出後にプレイヤーの送り入力を待つ。</summary>
        public IEnumerator ShowLine(string speaker, string portrait, string text)
        {
            InitializePresenters();
            _rewardPresenter.HideAll();
            State = ConversationState.Entering;
            _chromePresenter.PrepareLineText(_nameText, _bodyText);
            yield return ShowAnimated();
            SetChoicesActive(false);
            bool narration = string.IsNullOrEmpty(portrait);
            var resolved = narration
                ? new DialoguePortraitPresenter.ResolvedPortrait(
                    null,
                    null,
                    DialoguePortraitSide.Left,
                    string.Empty,
                    new Color(0.78f, 0.82f, 0.9f, 1f),
                    null,
                    Vector2.zero
                )
                : _portraitPresenter.Resolve(
                    portrait,
                    _characters,
                    _portraits,
                    _defaultPortrait,
                    _defaultPortraitSide
                );
            yield return _portraitPresenter.Set(resolved);
            _rightPortrait = _portraitPresenter.RightPortrait;
            _currentTypingSound =
                resolved.TypingSound != null ? resolved.TypingSound : _typingSound;

            string displayName = !string.IsNullOrWhiteSpace(speaker)
                ? speaker
                : resolved.DisplayName;
            _currentLineId = $"{displayName}\n{portrait}\n{text}";
            _currentLineWasRead = _readLineIds.Contains(_currentLineId);
            if (_nameText != null)
            {
                _nameText.text = displayName;
                _nameText.color = resolved.ThemeColor;
                _nameText.gameObject.SetActive(!narration && !string.IsNullOrEmpty(displayName));
            }
            if (_bodyText != null)
                _bodyText.alignment = narration
                    ? TextAlignmentOptions.Center
                    : TextAlignmentOptions.MidlineLeft;
            _chromePresenter.PlayLineTextEntrance(
                _nameText,
                _bodyText,
                !narration && !string.IsNullOrEmpty(displayName)
            );

            EnsureHistoryPanel();
            bool historyPortraitObscured =
                !narration
                && (
                    _portraitPresenter.IsObscured(resolved.Side) || displayName is "？？？" or "???"
                );
            _historyPanel?.AddEntry(
                displayName,
                DialogueMarkupParser.Parse(text).Text,
                resolved.Icon,
                resolved.Side,
                historyPortraitObscured
            );

            yield return TypeBody(text ?? string.Empty);
            yield return WaitForAdvance();
            _readLineIds.Add(_currentLineId);
        }

        /// <summary>選択肢を提示し、選ばれた値を <paramref name="onSelected"/> で返す。</summary>
        public IEnumerator ShowChoice(
            IReadOnlyList<ChoiceOption> options,
            Action<string> onSelected
        )
        {
            InitializePresenters();
            _rewardPresenter.HideAll();
            State = ConversationState.ShowingChoices;
            SetWindowHidden(false);
            _chromePresenter.StopBounce();
            yield return ShowAnimated();

            _choicePresenter.Clear();
            _choicePresenter.SetActive(true);
            _chromePresenter.SetChoiceGuide(true);

            string picked = null;
            string pickedText = null;
            Button pickedButton = null;
            bool hasPicked = false;
            int choiceCount = _choicePresenter.Spawn(
                options,
                (value, optionText, button) =>
                {
                    picked = value;
                    pickedText = optionText;
                    pickedButton = button;
                    hasPicked = true;
                }
            );

            if (choiceCount == 0)
            {
                Debug.LogWarning("[ConversationView] 表示できる選択肢がありません。");
                SetChoicesActive(false);
                onSelected?.Invoke(null);
                State = ConversationState.Entering;
                yield break;
            }

            yield return _choicePresenter.AnimateIn();
            _choicePresenter.SelectFirst();

            while (!hasPicked)
                yield return null;

            yield return _choicePresenter.AnimateSelection(pickedButton);
            _choicePresenter.Clear();
            SetChoicesActive(false);
            EnsureHistoryPanel();
            _historyPanel?.AddChoiceEntry(options, pickedText);
            onSelected?.Invoke(picked);
            State = ConversationState.Entering;
        }

        /// <summary>
        /// 受け取ったアイテムの画像を表示し、送り入力まで待って片付ける。
        /// <paramref name="sprite"/> 未指定なら <see cref="_itemGetSprite"/>(ダミー)を使う。
        /// 画像は Prefab に要素を持たず実行時に Canvas 直下へ生成する(表示ロジックは常駐 UI 側に集約。
        /// 将来 EventPlayer の giveItem ステップから itemKey で解決した Sprite を渡す想定)。
        /// </summary>
        public IEnumerator ShowItemGet(Sprite sprite = null, string acquiredMessage = null)
        {
            InitializePresenters();
            State = ConversationState.Entering;
            yield return ShowAnimated();
            SetChoicesActive(false);
            _rewardPresenter.ShowItem(
                sprite != null ? sprite : _itemGetSprite,
                _itemGetPosition,
                _itemGetSize
            );
            yield return _rewardPresenter.AnimateItemIn();
            yield return WaitForAdvance();
            yield return _rewardPresenter.AnimateItemOut();
            _rewardPresenter.HideItem();
            if (!string.IsNullOrWhiteSpace(acquiredMessage))
                yield return ShowLine(null, null, acquiredMessage);
        }

        /// <summary>
        /// 受け取った武器の3Dモデルを RenderTexture 経由でUIに表示し、送り入力まで回転させて待つ。
        /// アイテム画像(<see cref="ShowItemGet"/>)と同じ位置・サイズに出す。カメラ/ライト/モデルのリグは
        /// シーンから離した場所へ実行時に組んで終了で破棄する(常駐なし・シーン非依存・専用レイヤー不要)。
        /// <paramref name="modelPrefab"/> 未指定なら <see cref="_weaponModelPrefab"/>(ダミー)。将来は
        /// EventPlayer の giveWeapon ステップから weaponKey で解決した Prefab を渡す想定。
        /// </summary>
        public IEnumerator ShowWeaponGet(
            GameObject modelPrefab = null,
            string acquiredMessage = null
        )
        {
            InitializePresenters();
            State = ConversationState.Entering;
            yield return ShowAnimated();
            SetChoicesActive(false);

            var model = _rewardPresenter.ShowWeapon(
                modelPrefab != null ? modelPrefab : _weaponModelPrefab,
                _weaponModelEuler,
                _weaponTextureSize,
                _weaponImageSize,
                _itemGetPosition,
                _weaponFrameFill,
                _weaponBackdropColor
            );
            if (model == null)
            {
                yield return WaitForAdvance();
                yield break;
            }

            yield return _rewardPresenter.AnimateWeaponIn();
            yield return WaitForAdvance(); // 回転させず静止表示。送り入力まで待つ
            yield return _rewardPresenter.AnimateWeaponOut();
            _rewardPresenter.HideWeapon();
            if (!string.IsNullOrWhiteSpace(acquiredMessage))
                yield return ShowLine(null, null, acquiredMessage);
        }

        // ---- 内部 ----

        private IEnumerator TypeBody(string text)
        {
            State = ConversationState.Typing;
            _chromePresenter?.StopBounce();

            yield return _textPlayer.Play(
                _bodyText,
                text,
                _textSpeed,
                _charInterval,
                _punctuationDelay,
                _fastForwardMultiplier,
                _currentLineWasRead,
                _typingAudioSource,
                _currentTypingSound,
                () => _skipMode,
                () => _windowManuallyHidden
            );
        }

        private IEnumerator WaitForAdvance()
        {
            State = ConversationState.WaitingForAdvance;
            _chromePresenter.StartBounce();
            yield return _advanceController.Wait(
                _bodyText,
                _currentLineWasRead,
                () => IsAutoMode && !_windowManuallyHidden,
                () => _skipMode,
                () => _windowManuallyHidden,
                () => _historyPanel != null && _historyPanel.IsOpen,
                _autoAdvanceDelay
            );
            _chromePresenter.StopBounce();
            State = ConversationState.Entering;
        }

        public void SetAutoMode(bool enabled)
        {
            if (IsAutoMode == enabled)
                return;

            IsAutoMode = enabled;
            InitializePresenters();
            _chromePresenter.SetAutoMode(enabled);
            SetControlButtonActive(_autoControlButton, enabled);
        }

        private void BindControlButtons()
        {
            if (_controlsBound)
                return;
            _controlsBound = true;
            _autoControlButton?.onClick.AddListener(() => SetAutoMode(!IsAutoMode));
            _skipControlButton?.onClick.AddListener(() =>
            {
                _skipMode = !_skipMode;
                SetControlButtonActive(_skipControlButton, _skipMode);
            });
            _speedControlButton?.onClick.AddListener(() =>
                SetTextSpeed((TextSpeed)(((int)_textSpeed + 1) % 4))
            );
            _hideControlButton?.onClick.AddListener(() => SetWindowHidden(!_windowManuallyHidden));
        }

        private static void SetControlButtonActive(Button button, bool active)
        {
            if (button == null)
                return;
            if (button.TryGetComponent<ConversationControlButton>(out var control))
                control.SetActiveState(active);
            else if (button.targetGraphic != null)
                button.targetGraphic.color = active
                    ? new Color(0.16f, 0.42f, 0.62f, 0.96f)
                    : new Color(0.035f, 0.045f, 0.07f, 0.86f);
        }

        private static void PlayControlShortcut(Button button)
        {
            if (
                button != null
                && button.TryGetComponent<ConversationControlButton>(out var control)
            )
                control.PlayShortcutFeedback();
        }

        public void SetWindowHidden(bool hidden)
        {
            _windowManuallyHidden = hidden;
            InitializePresenters();
            _chromePresenter.SetWindowHidden(hidden);
            SetControlButtonActive(_hideControlButton, hidden);
        }

        public void SetTextSpeed(TextSpeed speed)
        {
            _textSpeed = speed;
            InitializePresenters();
            _chromePresenter.SetTextSpeed(speed);
            UpdateSpeedControlLabel();
            ShowSpeedToast();
        }

        private void UpdateSpeedControlLabel()
        {
            if (_speedControlLabel == null)
                return;
            _speedControlLabel.text = _textSpeed switch
            {
                TextSpeed.Slow => "SPEED x0.6",
                TextSpeed.Fast => "SPEED x2",
                TextSpeed.Instant => "SPEED MAX",
                _ => "SPEED x1",
            };
        }

        private void ShowSpeedToast()
        {
            if (_speedToast == null || !Application.isPlaying)
                return;
            if (_speedToastRoutine != null)
                StopCoroutine(_speedToastRoutine);
            _speedToast.text = _textSpeed switch
            {
                TextSpeed.Slow => "TEXT SPEED  x0.6",
                TextSpeed.Fast => "TEXT SPEED  x2",
                TextSpeed.Instant => "TEXT SPEED  MAX",
                _ => "TEXT SPEED  x1",
            };
            _speedToastRoutine = StartCoroutine(AnimateSpeedToast());
        }

        private IEnumerator AnimateSpeedToast()
        {
            var group = _speedToast.GetComponent<CanvasGroup>();
            var rect = _speedToast.rectTransform;
            Vector2 basePosition = rect.anchoredPosition;
            const float duration = 0.7f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float normalized = elapsed / duration;
                group.alpha =
                    normalized < 0.18f
                        ? normalized / 0.18f
                        : 1f - Mathf.InverseLerp(0.62f, 1f, normalized);
                rect.anchoredPosition = basePosition + Vector2.up * (7f * normalized);
                yield return null;
            }
            group.alpha = 0f;
            rect.anchoredPosition = basePosition;
            _speedToastRoutine = null;
        }

        public bool IsLineRead(string speaker, string portrait, string text) =>
            _readLineIds.Contains($"{speaker}\n{portrait}\n{text}");

        public void MarkLineRead(string speaker, string portrait, string text) =>
            _readLineIds.Add($"{speaker}\n{portrait}\n{text}");

        public void ClearReadHistory() => _readLineIds.Clear();

        public void SetPortraitVisible(DialoguePortraitSide side, bool visible)
        {
            InitializePresenters();
            _portraitPresenter.SetVisible(side, visible);
        }

        public IEnumerator PlayPortraitEffect(
            DialoguePortraitSide side,
            PortraitEffect effect,
            float duration = 0.28f
        )
        {
            InitializePresenters();
            yield return _portraitPresenter.PlayEffect(side, effect, duration);
        }

        public IEnumerator SetPortraitObscured(
            DialoguePortraitSide side,
            bool obscured,
            float duration = 0.5f
        )
        {
            InitializePresenters();
            yield return _portraitPresenter.SetObscured(side, obscured, duration);
        }

        public IEnumerator RunPresentationCommand(string command, string argument = null)
        {
            switch (command?.Trim().ToLowerInvariant())
            {
                case "window.hide":
                    SetWindowHidden(true);
                    break;
                case "window.show":
                    SetWindowHidden(false);
                    break;
                case "portrait.left.hide":
                    SetPortraitVisible(DialoguePortraitSide.Left, false);
                    break;
                case "portrait.right.hide":
                    SetPortraitVisible(DialoguePortraitSide.Right, false);
                    break;
                case "portrait.left.obscure":
                    yield return SetPortraitObscured(DialoguePortraitSide.Left, true);
                    break;
                case "portrait.right.obscure":
                    yield return SetPortraitObscured(DialoguePortraitSide.Right, true);
                    break;
                case "portrait.left.reveal":
                    yield return SetPortraitObscured(DialoguePortraitSide.Left, false);
                    break;
                case "portrait.right.reveal":
                    yield return SetPortraitObscured(DialoguePortraitSide.Right, false);
                    break;
                case "portrait.left.shake":
                    yield return PlayPortraitEffect(
                        DialoguePortraitSide.Left,
                        PortraitEffect.Shake
                    );
                    break;
                case "portrait.right.shake":
                    yield return PlayPortraitEffect(
                        DialoguePortraitSide.Right,
                        PortraitEffect.Shake
                    );
                    break;
                case "portrait.left.jump":
                    yield return PlayPortraitEffect(DialoguePortraitSide.Left, PortraitEffect.Jump);
                    break;
                case "portrait.right.jump":
                    yield return PlayPortraitEffect(
                        DialoguePortraitSide.Right,
                        PortraitEffect.Jump
                    );
                    break;
                case "wait":
                    if (
                        float.TryParse(
                            argument,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float seconds
                        )
                    )
                        yield return new WaitForSecondsRealtime(Mathf.Clamp(seconds, 0f, 10f));
                    break;
                case "conversation.close":
                    yield return HideAnimated();
                    break;
                default:
                    if (
                        command != null
                        && (
                            command.StartsWith("camera.", StringComparison.OrdinalIgnoreCase)
                            || command.StartsWith("background.", StringComparison.OrdinalIgnoreCase)
                        )
                    )
                        ExternalPresentationCommandRequested?.Invoke(command, argument);
                    else
                        Debug.LogWarning($"[ConversationView] 未対応の演出コマンドです: {command}");
                    break;
            }
        }

        private void SetChoicesActive(bool active)
        {
            _choicePresenter?.SetActive(active);
            _chromePresenter?.SetChoiceGuide(active);
        }

        private void EnsureHistoryPanel()
        {
            if (_historyPanel == null)
                _historyPanel = GetComponent<DialogueHistoryPanel>();
            if (_historyPanel == null)
                _historyPanel = gameObject.AddComponent<DialogueHistoryPanel>();

            _historyPanel.Initialize(_nameText != null ? _nameText.font : null);
            if (!_historyEventsBound)
            {
                _historyPanel.Closed += HandleHistoryClosed;
                _historyEventsBound = true;
            }
        }

        private void HandleHistoryClosed()
        {
            if (isActiveAndEnabled)
                StartCoroutine(_portraitPresenter.PlayReturnFocus());
        }

        private IEnumerator ShowAnimated()
        {
            InitializePresenters();
            yield return _chromePresenter.Show();
        }

        public IEnumerator HideAnimated(float duration = 0.2f)
        {
            State = ConversationState.Exiting;
            InitializePresenters();
            float textDuration = Mathf.Max(0.05f, duration * 0.35f);
            float portraitDuration = Mathf.Max(0.06f, duration * 0.4f);
            float windowDuration = Mathf.Max(0.07f, duration * 0.45f);
            yield return _chromePresenter.HideLineText(textDuration);
            yield return _portraitPresenter.FadeOutAll(portraitDuration);
            yield return _chromePresenter.Hide(windowDuration);
            _rewardPresenter.HideAll();
            _choicePresenter.Clear();
            _choicePresenter.SetActive(false);
            State = ConversationState.Hidden;
        }

        private void HideImmediate()
        {
            State = ConversationState.Exiting;
            _rewardPresenter?.HideAll();
            SetChoicesActive(false);
            _chromePresenter?.HideImmediate();
            State = ConversationState.Hidden;
        }
    }
}
