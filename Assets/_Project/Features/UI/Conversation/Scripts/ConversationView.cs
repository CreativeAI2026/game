using System;
using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 会話UIの実体。<see cref="IDialogueView"/> を実装し、生成時に <see cref="DialogueViewService.Current"/> へ自身を登録する。
    /// UIRoot Prefab の子として同梱され、常駐・DDOL は <see cref="UIRoot"/> が担う。状態は保存しない。
    /// 再生時は Awake でウィンドウを隠し、編集時は Prefab の見た目がそのままプレビューになる。
    /// </summary>
    public sealed class ConversationView : MonoBehaviour, IDialogueView
    {
        public static ConversationView Instance { get; private set; }

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

        /// <summary>会話ログを開いているか。</summary>
        internal bool IsHistoryOpen => _historyPanel != null && _historyPanel.IsOpen;

        /// <summary>プレイヤーの操作でウィンドウを隠しているか。</summary>
        internal bool IsWindowHidden => _windowManuallyHidden;

        /// <summary>入手表示を出しているか。</summary>
        internal bool IsRewardShowing => _rewardPresentationActive;

        /// <summary>今の文字送りの速さ。</summary>
        internal TextSpeed CurrentTextSpeed => _textSpeed;

        /// <summary>選択肢・持ち物ピッカーへキー入力を渡す。</summary>
        internal void HandleChoiceKeys()
        {
            _choices.HandleKeyboardInput();
            _itemPicker?.HandleKeyboardInput();
        }

        [Header("部品")]
        [SerializeField]
        private DialoguePortraitView _portraits;

        [SerializeField]
        private DialogueChoiceView _choices;

        [SerializeField]
        private DialogueRewardView _rewards;

        [Header("ルート表示")]
        [SerializeField]
        private CanvasGroup _root; // ウィンドウ全体の表示/非表示。非会話時は alpha=0

        [SerializeField]
        private RectTransform _windowRoot;

        [Header("表示要素")]
        [SerializeField]
        private TMP_Text _nameText; // 名前プレート

        [SerializeField]
        private TMP_Text _bodyText; // 本文

        [SerializeField]
        private GameObject _nextIndicator; // 送り待ちに小さくバウンドするインジケーター

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

        [Header("演出コマンドの素材(events.json にはキーだけ書く)")]
        [SerializeField]
        [Tooltip("sound.se / sound.bgm のキーと SoundCue(Audio/SoundCue に置いたものを割り当てる)")]
        private DialogueSoundEntry[] _sounds = Array.Empty<DialogueSoundEntry>();

        [SerializeField]
        [Tooltip("background.image のキーと背景画像")]
        private DialogueBackgroundEntry[] _backgrounds = Array.Empty<DialogueBackgroundEntry>();

        [Header("操作ボタン")]
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

        private DialogueItemPickerPresenter _itemPicker;
        private readonly DialogueTextPlayer _textPlayer = new();
        private ConversationChromePresenter _chromePresenter;
        private readonly ConversationControlsPresenter _controlsPresenter = new();
        private ConversationSpeedPresenter _speedPresenter;
        private DialoguePresentationCommandRouter _presentationCommandRouter;
        private DialogueBackdropPresenter _backdropPresenter;
        private readonly DialogueAdvanceController _advanceController = new();
        private ConversationInputController _inputController;
        private bool _windowManuallyHidden;
        private readonly DialogueSessionState _sessionState = new();
        private AudioClip _currentTypingSound;
        private bool _skipMode;
        private bool _rewardPresentationActive;
        private bool _historyEventsBound;
        private float _advanceBlockedUntil;

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
            _chromePresenter.SetAutoMode(IsAutoMode);
            _speedPresenter.SetSpeed(_textSpeed, false);
            BindControlButtons();
            EnsureHistoryPanel();
            HideImmediate(); // 会話開始まで隠す(編集時は Awake が走らずプレビューが見える)
        }

        private void InitializePresenters()
        {
            ResolveParts();
            _chromePresenter ??= new ConversationChromePresenter(this);
            _speedPresenter ??= new ConversationSpeedPresenter(this);
            _speedPresenter.Configure(_speedControlLabel, _speedToast);
            _controlsPresenter.Configure(
                _autoControlButton,
                _skipControlButton,
                _speedControlButton,
                _hideControlButton
            );
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
            _inputController ??= new ConversationInputController(
                this,
                _controlsPresenter,
                _chromePresenter,
                _advanceController
            );
            _backdropPresenter ??= new DialogueBackdropPresenter(
                _root != null ? _root.transform : transform
            );
            _presentationCommandRouter ??= new DialoguePresentationCommandRouter(
                this,
                _portraits,
                new DialogueStageCommands(_backdropPresenter, FindSound, FindBackground)
            );
            _chromePresenter.EnsureView();
            ConfigureTextOverflow();
            _autoModeIndicator = _chromePresenter.AutoIndicator;
            _controlGuide = _chromePresenter.ControlGuide;
            _autoProgressFill = _chromePresenter.AutoProgress;
        }

        // 部品が未設定なら同じ GameObject から探し、無ければ足す(テストや組み立て途中でも落ちないように)
        private void ResolveParts()
        {
            _portraits = GetOrAdd(_portraits);
            _choices = GetOrAdd(_choices);
            _rewards = GetOrAdd(_rewards);
        }

        private T GetOrAdd<T>(T current)
            where T : Component
        {
            if (current != null)
                return current;
            return TryGetComponent(out T found) ? found : gameObject.AddComponent<T>();
        }

        private void ConfigureTextOverflow()
        {
            if (_nameText != null)
            {
                _nameText.enableAutoSizing = true;
                _nameText.fontSizeMin = 24f;
                _nameText.fontSizeMax = 36f;
                _nameText.textWrappingMode = TextWrappingModes.NoWrap;
            }
            if (_bodyText != null)
            {
                _bodyText.enableAutoSizing = true;
                _bodyText.fontSizeMin = 24f;
                _bodyText.fontSizeMax = 34f;
                _bodyText.textWrappingMode = TextWrappingModes.Normal;
            }
        }

        private void Update()
        {
            _inputController?.Tick();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (ReferenceEquals(DialogueViewService.Current, this))
                DialogueViewService.Current = null;
        }

        private void OnDisable()
        {
            _speedPresenter?.Cancel();
            _itemPicker?.Dismiss();
            _rewardPresentationActive = false;
        }

        /// <summary>1行を立ち絵付きで表示し、タイプライター送出後にプレイヤーの送り入力を待つ。</summary>
        public IEnumerator ShowLine(string speaker, string portrait, string text)
        {
            InitializePresenters();
            CancelRewardPresentation();
            State = ConversationState.Entering;
            _chromePresenter.PrepareLineText(_nameText, _bodyText);
            yield return ShowAnimated();
            SetChoicesActive(false);
            yield return PrepareLine(speaker, portrait, text);

            yield return TypeBody(text ?? string.Empty);
            var parsed = DialogueMarkupParser.Parse(text);
            if (parsed.HasAutoAdvance)
                yield return WaitAutoAdvance(parsed.AutoAdvanceSeconds);
            else
                yield return WaitForAdvance();
            _sessionState.MarkCurrentLineRead();
        }

        /// <summary>
        /// &lt;auto=秒&gt; の行: 表示しきってから秒数だけ待って、送り入力なしで次へ進む。
        /// 送り入力を押せばその場で進める。通常の送り待ち(<see cref="WaitForAdvance"/>)と同じく、
        /// ウィンドウを隠している間・ログを開いている間・入力ブロック中は、待ち時間を数えず、送り入力も受け付けない。
        /// 既読スキップと開発用の全スキップのときは、待たずに進む。
        /// </summary>
        private IEnumerator WaitAutoAdvance(float seconds)
        {
            State = ConversationState.WaitingForAdvance;
            yield return null;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                bool blocked =
                    _windowManuallyHidden
                    || IsHistoryOpen
                    || Time.unscaledTime < _advanceBlockedUntil;
                if (!blocked)
                {
                    if (
                        DialogueAdvanceController.AdvancePressed()
                        || DialogueDebugSkip.Active
                        || (_sessionState.CurrentLineWasRead && _skipMode)
                    )
                        break;
                    elapsed += Time.unscaledDeltaTime;
                }
                yield return null;
            }
            State = ConversationState.Entering;
        }

        /// <summary>選択肢を提示し、選ばれた値を <paramref name="onSelected"/> で返す。</summary>
        public IEnumerator ShowChoice(
            IReadOnlyList<ChoiceOption> options,
            Action<string> onSelected
        )
        {
            InitializePresenters();
            CancelRewardPresentation();
            State = ConversationState.ShowingChoices;
            SetWindowHidden(false);
            _chromePresenter.StopBounce();
            yield return ShowAnimated();

            _chromePresenter.SetChoiceGuide(true);
            bool hasPicked = false;
            string picked = null;
            string pickedText = null;
            yield return _choices.Choose(
                options,
                _sessionState.SelectedChoiceValues,
                (value, text) =>
                {
                    hasPicked = true;
                    picked = value;
                    pickedText = text;
                }
            );
            _chromePresenter.SetChoiceGuide(false);
            if (hasPicked)
            {
                EnsureHistoryPanel();
                _historyPanel.AddChoiceEntry(options, pickedText);
                _sessionState.MarkChoiceSelected(picked);
                BlockAdvanceInput(0.15f);
            }
            onSelected?.Invoke(picked);
            State = ConversationState.Entering;
        }

        /// <summary>
        /// IDialogueView 実装: useItem ステップの持ち物ピッカー。未装備の持ち物の一覧と「やめる」を出し、
        /// 選んだ itemKey(やめたら null、キーを持たないアイテムは空文字)を <paramref name="onPicked"/> で返す。
        /// </summary>
        public IEnumerator ShowItemPicker(string prompt, Action<string> onPicked)
        {
            InitializePresenters();
            CancelRewardPresentation();
            State = ConversationState.ShowingChoices; // 送り入力を止め、キー入力をピッカーへ回す
            SetWindowHidden(false);
            _chromePresenter.StopBounce();
            yield return ShowAnimated();

            _itemPicker ??= new DialogueItemPickerPresenter(
                _root != null ? _root.transform : transform,
                _nameText != null ? _nameText.font : null
            );
            var inventory = InventoryManager.Instance;
            var entries = ItemPickerEntries.Build(
                inventory != null ? inventory.GetAllItems() : null
            );

            bool hasPicked = false;
            string picked = null;
            _itemPicker.Show(
                prompt,
                entries,
                value =>
                {
                    picked = value;
                    hasPicked = true;
                }
            );
            while (!hasPicked)
            {
                // 選ぶ前にこの UI ごとシーンが消えたら、やめた扱いで返す(待ち続けるとイベントが終わらず操作が戻らない)。
                if (this == null)
                {
                    onPicked?.Invoke(null);
                    yield break;
                }
                yield return null;
            }

            _itemPicker.Clear();
            State = ConversationState.Entering;
            BlockAdvanceInput(0.15f);
            onPicked?.Invoke(picked);
        }

        /// <summary>
        /// 受け取ったアイテムの画像を表示し、送り入力まで待って片付ける。<paramref name="sprite"/> 未指定なら既定の画像を出す。
        /// </summary>
        public IEnumerator ShowItemGet(Sprite sprite = null, string acquiredMessage = null)
        {
            InitializePresenters();
            CancelRewardPresentation();
            _rewardPresentationActive = true;
            State = ConversationState.Entering;
            yield return ShowAnimated();
            SetChoicesActive(false);
            _rewards.ShowItem(sprite);
            yield return PlayReward(
                _rewards.AnimateItemIn(),
                _rewards.AnimateItemOut(),
                acquiredMessage,
                false
            );
            _rewards.HideItem();
            _rewardPresentationActive = false;
        }

        /// <summary>
        /// 受け取った武器の3Dモデルを <see cref="ShowItemGet"/> と同じ位置に表示し、送り入力まで待って片付ける。
        /// </summary>
        public IEnumerator ShowWeaponGet(
            GameObject modelPrefab = null,
            string acquiredMessage = null
        )
        {
            InitializePresenters();
            CancelRewardPresentation();
            _rewardPresentationActive = true;
            State = ConversationState.Entering;
            yield return ShowAnimated();
            SetChoicesActive(false);

            if (!_rewards.ShowWeapon(modelPrefab))
            {
                _rewardPresentationActive = false;
                yield break;
            }

            yield return PlayReward(
                _rewards.AnimateWeaponIn(),
                _rewards.AnimateWeaponOut(),
                acquiredMessage,
                true
            );
            _rewards.HideWeapon();
            _rewardPresentationActive = false;
        }

        // ---- 内部 ----

        // 立ち絵・名前・本文の見た目を1行分そろえ、既読判定と履歴への記録をする
        private IEnumerator PrepareLine(string speaker, string portrait, string text)
        {
            bool narration = string.IsNullOrEmpty(portrait);
            var resolved = _portraits.Resolve(portrait);
            yield return _portraits.Set(resolved);
            _currentTypingSound =
                resolved.TypingSound != null ? resolved.TypingSound : _typingSound;

            string displayName = !string.IsNullOrWhiteSpace(speaker)
                ? speaker
                : resolved.DisplayName;
            bool showName = !narration && !string.IsNullOrEmpty(displayName);
            _sessionState.BeginLine(displayName, portrait, text);
            if (_nameText != null)
            {
                _nameText.text = displayName;
                _nameText.color = resolved.ThemeColor;
                _nameText.gameObject.SetActive(showName);
            }
            if (_bodyText != null)
                _bodyText.alignment = narration
                    ? TextAlignmentOptions.Center
                    : TextAlignmentOptions.MidlineLeft;
            _chromePresenter.PlayLineTextEntrance(_nameText, _bodyText, showName);

            bool portraitObscured =
                !narration
                && (_portraits.IsObscured(resolved.Side) || displayName is "？？？" or "???");
            EnsureHistoryPanel();
            _historyPanel.AddEntry(
                displayName,
                DialogueMarkupParser.Parse(text).Text,
                resolved.Icon,
                resolved.Side,
                portraitObscured
            );
        }

        // 入手表示の登場と同時にメッセージを打ち、送り入力を待ってから一緒に退場させる
        private IEnumerator PlayReward(
            IEnumerator animateIn,
            IEnumerator animateOut,
            string message,
            bool weapon
        )
        {
            bool hasMessage = !string.IsNullOrWhiteSpace(message);
            PrepareRewardText(hasMessage ? message : null, weapon);
            Coroutine entrance = StartCoroutine(animateIn);
            if (hasMessage)
                yield return TypeBody(message);
            yield return entrance;

            yield return WaitForAdvance(); // AUTO中も表示時間を確保してから次へ進む

            Coroutine textExit = hasMessage
                ? StartCoroutine(_chromePresenter.HideLineText(0.18f))
                : null;
            yield return animateOut;
            if (textExit != null)
                yield return textExit;
            if (hasMessage)
                _sessionState.MarkCurrentLineRead();
        }

        // 入手メッセージは名前なし・中央寄せで出し、履歴にも入手として残す
        private void PrepareRewardText(string message, bool weapon)
        {
            _chromePresenter.PrepareLineText(_nameText, _bodyText);
            if (_nameText != null)
            {
                _nameText.text = string.Empty;
                _nameText.gameObject.SetActive(false);
            }
            if (_bodyText != null)
            {
                _bodyText.text = string.Empty;
                _bodyText.alignment = TextAlignmentOptions.Center;
            }
            if (message == null)
                return;
            _currentTypingSound = _typingSound;
            _sessionState.BeginReward(message);
            _chromePresenter.PlayLineTextEntrance(_nameText, _bodyText, false);
            EnsureHistoryPanel();
            _historyPanel.AddRewardEntry(DialogueMarkupParser.Parse(message).Text, weapon);
        }

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
                _sessionState.CurrentLineWasRead || DialogueDebugSkip.Active,
                _typingAudioSource,
                _currentTypingSound,
                () => _skipMode || DialogueDebugSkip.Active,
                () => _windowManuallyHidden
            );
        }

        private IEnumerator WaitForAdvance(bool allowAuto = true)
        {
            State = ConversationState.WaitingForAdvance;
            _chromePresenter.StartBounce();
            yield return _advanceController.Wait(
                _bodyText,
                _sessionState.CurrentLineWasRead || DialogueDebugSkip.Active,
                () => allowAuto && IsAutoMode && !_windowManuallyHidden,
                () => _skipMode || DialogueDebugSkip.Active,
                () => _windowManuallyHidden || Time.unscaledTime < _advanceBlockedUntil,
                () => IsHistoryOpen,
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
            _controlsPresenter.SetAutoActive(enabled);
        }

        private void BindControlButtons()
        {
            _controlsPresenter.Bind(
                () => SetAutoMode(!IsAutoMode),
                () =>
                {
                    _skipMode = !_skipMode;
                    _controlsPresenter.SetSkipActive(_skipMode);
                },
                () => SetTextSpeed((TextSpeed)(((int)_textSpeed + 1) % 4)),
                () => SetWindowHidden(!_windowManuallyHidden)
            );
        }

        public void SetWindowHidden(bool hidden)
        {
            _windowManuallyHidden = hidden;
            InitializePresenters();
            _chromePresenter.SetWindowHidden(hidden);
            _controlsPresenter.SetHideActive(hidden);
        }

        public void SetTextSpeed(TextSpeed speed)
        {
            _textSpeed = speed;
            InitializePresenters();
            _chromePresenter.SetTextSpeed(speed);
            _speedPresenter.SetSpeed(speed, true);
        }

        public bool IsLineRead(string speaker, string portrait, string text) =>
            _sessionState.IsLineRead(speaker, portrait, text);

        public void MarkLineRead(string speaker, string portrait, string text) =>
            _sessionState.MarkLineRead(speaker, portrait, text);

        public void ClearReadHistory() => _sessionState.ClearReadHistory();

        public void SetPortraitVisible(DialoguePortraitSide side, bool visible)
        {
            InitializePresenters();
            _portraits.SetVisible(side, visible);
        }

        public IEnumerator PlayPortraitEffect(
            DialoguePortraitSide side,
            PortraitEffect effect,
            float duration = 0.28f
        )
        {
            InitializePresenters();
            yield return _portraits.PlayEffect(side, effect, duration);
        }

        public IEnumerator SetPortraitObscured(
            DialoguePortraitSide side,
            bool obscured,
            float duration = 0.5f
        )
        {
            InitializePresenters();
            yield return _portraits.SetObscured(side, obscured, duration);
        }

        // 登録した素材からキーで引く。同じキーが複数あれば先に登録したほうを使う。
        private SoundCue FindSound(string key)
        {
            foreach (var entry in _sounds ?? Array.Empty<DialogueSoundEntry>())
                if (entry.Key == key)
                    return entry.Cue;
            return null;
        }

        private Sprite FindBackground(string key)
        {
            foreach (var entry in _backgrounds ?? Array.Empty<DialogueBackgroundEntry>())
                if (entry.Key == key)
                    return entry.Sprite;
            return null;
        }

        public IEnumerator RunPresentationCommand(string command, string argument = null)
        {
            InitializePresenters();
            yield return _presentationCommandRouter.Execute(command, argument);
        }

        /// <summary>
        /// IDialogueView 実装: giveItem ステップの入手演出。itemKey→アイコン/名前の解決はここで行う
        /// (見せ方は UI の担当なので、EventPlayer はキーだけ渡す)。
        /// message 省略時は「〜を手に入れた。」を名前から組み立てる。
        /// </summary>
        public IEnumerator ShowItemGet(string itemKey, string message)
        {
            var data =
                ItemCatalog.Instance != null ? ItemCatalog.Instance.GetItemByKey(itemKey) : null;
            string body =
                !string.IsNullOrWhiteSpace(message) ? message
                : data != null && !string.IsNullOrEmpty(data.itemName)
                    ? $"{data.itemName}を手に入れた。"
                : null;
            yield return ShowItemGet(data != null ? data.icon : null, body);
        }

        /// <summary>
        /// IDialogueView 実装: giveWeapon ステップの入手演出。3Dモデルは WeaponData がまだ参照を
        /// 持たないため解決できず、モデルなしで表示する(モデル参照は武器を持つ側=WeaponManager 班の担当)。
        /// </summary>
        public IEnumerator ShowWeaponGet(string weaponKey, string message)
        {
            yield return ShowWeaponGet((GameObject)null, message);
        }

        /// <summary>IDialogueView 実装: command ステップの演出コマンド。</summary>
        public IEnumerator RunCommand(string command, string argument) =>
            RunPresentationCommand(command, argument);

        public IEnumerator Close()
        {
            if (State == ConversationState.Hidden)
                yield break;
            yield return HideAnimated();
        }

        private void SetChoicesActive(bool active)
        {
            _choices.SetActive(active);
            _chromePresenter?.SetChoiceGuide(active);
        }

        private void EnsureHistoryPanel()
        {
            if (_historyPanel == null)
                TryGetComponent(out _historyPanel);
            if (_historyPanel == null)
                _historyPanel = gameObject.AddComponent<DialogueHistoryPanel>();

            _historyPanel.Initialize(_nameText != null ? _nameText.font : null);
            if (!_historyEventsBound)
            {
                _historyPanel.Closed += HandleHistoryClosed;
                _historyEventsBound = true;
            }
        }

        private void HandleHistoryClosed() => BlockAdvanceInput(0.12f);

        private void BlockAdvanceInput(float duration)
        {
            _advanceBlockedUntil = Mathf.Max(
                _advanceBlockedUntil,
                Time.unscaledTime + Mathf.Max(0f, duration)
            );
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
            yield return _portraits.FadeOutAll(portraitDuration);
            yield return _chromePresenter.Hide(windowDuration);
            _rewards.HideAll();
            _choices.Clear();
            _choices.SetActive(false);
            _itemPicker?.Dismiss();
            ResetTransientModes();
            State = ConversationState.Hidden;
        }

        private void HideImmediate()
        {
            State = ConversationState.Exiting;
            _rewards.HideAll();
            SetChoicesActive(false);
            _itemPicker?.Dismiss();
            ResetTransientModes();
            _chromePresenter?.HideImmediate();
            State = ConversationState.Hidden;
        }

        private void ResetTransientModes()
        {
            _backdropPresenter?.Reset();
            _rewardPresentationActive = false;
            _skipMode = false;
            _windowManuallyHidden = false;
            _advanceBlockedUntil = 0f;
            _controlsPresenter.SetSkipActive(false);
            _controlsPresenter.SetHideActive(false);
            if (_historyPanel != null && _historyPanel.IsOpen)
                _historyPanel.SetOpen(false);
        }

        private void CancelRewardPresentation()
        {
            _rewards.HideAll();
            _rewardPresentationActive = false;
        }
    }

    /// <summary>
    /// 演出コマンド(sound.se / sound.bgm)のキーと SoundCue の対応。ConversationView に登録する。
    /// SoundCue 自体は Audio/SoundCue に置いたまま Inspector で割り当てる(SoundKit の使い方と同じ)。
    /// </summary>
    [Serializable]
    public struct DialogueSoundEntry
    {
        public string Key;
        public SoundCue Cue;
    }

    /// <summary>演出コマンド(background.image)のキーと背景画像の対応。ConversationView に登録する。</summary>
    [Serializable]
    public struct DialogueBackgroundEntry
    {
        public string Key;
        public Sprite Sprite;
    }
}
