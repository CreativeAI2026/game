using System;
using System.Collections;
using System.Collections.Generic;
using CreativeAI.Core.EventSystem;
using TMPro;
using UnityEngine;
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
    public sealed partial class ConversationView : MonoBehaviour, IDialogueView
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
        private readonly ConversationControlsPresenter _controlsPresenter = new();
        private ConversationSpeedPresenter _speedPresenter;
        private DialoguePresentationCommandRouter _presentationCommandRouter;
        private readonly DialogueAdvanceController _advanceController = new();
        private DialogueRewardPresenter _rewardPresenter;
        private DialogueRewardFlow _rewardFlow;
        private DialogueLineFlow _lineFlow;
        private DialogueChoiceFlow _choiceFlow;
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
            _portraitPresenter.EnsureSlots();
            _rightPortrait = _portraitPresenter.RightPortrait;
            _portraitPresenter.HideImmediate();
            _chromePresenter.SetAutoMode(IsAutoMode);
            _speedPresenter.SetSpeed(_textSpeed, false);
            BindControlButtons();
            EnsureHistoryPanel();
            HideImmediate(); // 会話開始まで隠す(編集時は Awake が走らずプレビューが見える)
        }

        private void InitializePresenters()
        {
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
            if (
                _choicePresenter == null
                || !_choicePresenter.Uses(_choiceContainer, _choiceButtonTemplate)
            )
            {
                _choicePresenter?.Clear();
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
            }
            _rewardPresenter ??= new DialogueRewardPresenter(
                transform,
                _itemRewardImage,
                _itemRewardBackdrop,
                _weaponRewardImage,
                _weaponRewardBackdrop
            );
            _rewardFlow ??= new DialogueRewardFlow(
                this,
                _chromePresenter,
                _nameText,
                _bodyText,
                _typingSound,
                _sessionState,
                () =>
                {
                    EnsureHistoryPanel();
                    return _historyPanel;
                },
                TypeBody,
                clip => _currentTypingSound = clip
            );
            _lineFlow ??= new DialogueLineFlow(
                _portraitPresenter,
                _chromePresenter,
                _nameText,
                _bodyText,
                _characters,
                _portraits,
                _defaultPortrait,
                _defaultPortraitSide,
                _typingSound,
                _sessionState,
                () =>
                {
                    EnsureHistoryPanel();
                    return _historyPanel;
                }
            );
            _choiceFlow ??= new DialogueChoiceFlow(
                _choicePresenter,
                _chromePresenter,
                _sessionState,
                () =>
                {
                    EnsureHistoryPanel();
                    return _historyPanel;
                },
                SetChoicesActive,
                BlockAdvanceInput
            );
            _inputController ??= new ConversationInputController(
                _controlsPresenter,
                _chromePresenter,
                _advanceController,
                () => State,
                () => _historyPanel != null && _historyPanel.IsOpen,
                () => _windowManuallyHidden,
                () => _rewardPresentationActive,
                () => IsAutoMode,
                () => _textSpeed,
                SetAutoMode,
                SetWindowHidden,
                SetTextSpeed,
                () => _choicePresenter?.HandleKeyboardInput()
            );
            _presentationCommandRouter ??= new DialoguePresentationCommandRouter(
                SetWindowHidden,
                SetPortraitVisible,
                SetPortraitObscured,
                PlayPortraitEffect,
                () => HideAnimated(),
                (command, argument) =>
                    ExternalPresentationCommandRequested?.Invoke(command, argument)
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
            ConfigureTextOverflow();
            _autoModeIndicator = _chromePresenter.AutoIndicator;
            _controlGuide = _chromePresenter.ControlGuide;
            _autoProgressFill = _chromePresenter.AutoProgress;
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
            _rewardPresenter?.HideAll();
            if (Instance == this)
                Instance = null;
            if (ReferenceEquals(DialogueViewService.Current, this))
                DialogueViewService.Current = null;
        }

        private void OnDisable()
        {
            _speedPresenter?.Cancel();
            _rewardPresenter?.HideAll();
            _choicePresenter?.Clear();
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
            yield return _lineFlow.Prepare(speaker, portrait, text);
            _rightPortrait = _lineFlow.RightPortrait;
            _currentTypingSound = _lineFlow.TypingSound;

            yield return TypeBody(text ?? string.Empty);
            yield return WaitForAdvance();
            _sessionState.MarkCurrentLineRead();
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

            yield return _choiceFlow.Execute(options, onSelected);
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
            CancelRewardPresentation();
            _rewardPresentationActive = true;
            State = ConversationState.Entering;
            yield return ShowAnimated();
            SetChoicesActive(false);
            _rewardPresenter.ShowItem(
                sprite != null ? sprite : _itemGetSprite,
                _itemGetPosition,
                _itemGetSize
            );
            yield return _rewardFlow.Enter(
                _rewardPresenter.AnimateItemIn(),
                acquiredMessage,
                false
            );
            yield return WaitForAdvance();
            yield return _rewardFlow.Exit(_rewardPresenter.AnimateItemOut(), acquiredMessage);
            _rewardPresenter.HideItem();
            if (!string.IsNullOrWhiteSpace(acquiredMessage))
                _sessionState.MarkCurrentLineRead();
            _rewardPresentationActive = false;
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
            CancelRewardPresentation();
            _rewardPresentationActive = true;
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
                _rewardPresentationActive = false;
                yield break;
            }

            yield return _rewardFlow.Enter(
                _rewardPresenter.AnimateWeaponIn(),
                acquiredMessage,
                true
            );
            yield return WaitForAdvance(); // AUTO中も表示時間を確保してから次へ進む
            yield return _rewardFlow.Exit(_rewardPresenter.AnimateWeaponOut(), acquiredMessage);
            _rewardPresenter.HideWeapon();
            if (!string.IsNullOrWhiteSpace(acquiredMessage))
                _sessionState.MarkCurrentLineRead();
            _rewardPresentationActive = false;
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
                _sessionState.CurrentLineWasRead,
                _typingAudioSource,
                _currentTypingSound,
                () => _skipMode,
                () => _windowManuallyHidden
            );
        }

        private IEnumerator WaitForAdvance(bool allowAuto = true)
        {
            State = ConversationState.WaitingForAdvance;
            _chromePresenter.StartBounce();
            yield return _advanceController.Wait(
                _bodyText,
                _sessionState.CurrentLineWasRead,
                () => allowAuto && IsAutoMode && !_windowManuallyHidden,
                () => _skipMode,
                () => _windowManuallyHidden || Time.unscaledTime < _advanceBlockedUntil,
                () => _historyPanel != null && _historyPanel.IsOpen,
                _autoAdvanceDelay
            );
            _chromePresenter.StopBounce();
            State = ConversationState.Entering;
        }
    }
}
