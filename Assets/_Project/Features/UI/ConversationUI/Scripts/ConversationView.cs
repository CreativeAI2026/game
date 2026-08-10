using System;
using System.Collections;
using System.Collections.Generic;
using CreativeAI.Core.EventSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
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

        public enum ConversationState
        {
            Hidden,
            Entering,
            Typing,
            WaitingForAdvance,
            ShowingChoices,
            Exiting,
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
        private float _choiceBottomMargin = 64f; // 会話ウィンドウ上端から選択肢までの余白

        [Header("演出")]
        [SerializeField]
        private float _charInterval = 0.03f; // タイプライターの1文字あたり待ち時間(秒)

        [SerializeField]
        private float _punctuationDelay = 0.12f;

        [SerializeField]
        [Range(0.05f, 1f)]
        private float _fastForwardMultiplier = 0.2f;

        [SerializeField]
        private float _windowEnterDuration = 0.2f;

        [SerializeField]
        private float _windowEnterOffsetY = -24f;

        [Header("オートモード")]
        [SerializeField]
        private TMP_Text _autoModeIndicator;

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

        private readonly List<GameObject> _spawnedChoices = new();
        private Coroutine _indicatorBounce;
        private Vector2 _indicatorBasePosition;
        private bool _hasIndicatorBasePosition;
        private GameObject _itemGetObject; // ShowItemGet で実行時生成する画像
        private GameObject _weaponRigObject; // ShowWeaponGet のカメラ+ライト+モデル一式
        private GameObject _weaponRawImageObject; // ShowWeaponGet の Canvas 表示
        private GameObject _weaponBackdropObject; // ShowWeaponGet の背後パネル
        private RenderTexture _weaponRt;
        private bool _leftPortraitShown;
        private bool _rightPortraitShown;
        private Vector2 _windowBasePosition;
        private bool _hasWindowBasePosition;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // 二重生成ガード(冪等)。単一化は UIRoot が担うため通常起きない
                return;
            }
            Instance = this;

            DialogueViewService.Current = this; // EventPlayer が参照する seam へ自身を登録
            EnsurePortraitSlots();
            EnsureAutoModeIndicator();
            RefreshAutoModeIndicator();
            EnsureHistoryPanel();
            HideImmediate(); // 会話開始まで隠す(編集時は Awake が走らずプレビューが見える)
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.aKey.wasPressedThisFrame)
                SetAutoMode(!IsAutoMode);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (ReferenceEquals(DialogueViewService.Current, this))
                DialogueViewService.Current = null;
        }

        /// <summary>1行を立ち絵付きで表示し、タイプライター送出後にプレイヤーの送り入力を待つ。</summary>
        public IEnumerator ShowLine(string speaker, string portrait, string text)
        {
            State = ConversationState.Entering;
            yield return ShowAnimated();
            SetChoicesActive(false);
            var resolved = ResolvePortrait(portrait);
            yield return SetPortrait(resolved);

            string displayName = !string.IsNullOrWhiteSpace(speaker)
                ? speaker
                : resolved.DisplayName;
            if (_nameText != null)
                _nameText.text = displayName;

            EnsureHistoryPanel();
            _historyPanel?.AddEntry(displayName, text, resolved.Icon, resolved.Side);

            yield return TypeBody(text ?? string.Empty);
            yield return WaitForAdvance();
        }

        /// <summary>選択肢を提示し、選ばれた値を <paramref name="onSelected"/> で返す。</summary>
        public IEnumerator ShowChoice(
            IReadOnlyList<ChoiceOption> options,
            Action<string> onSelected
        )
        {
            State = ConversationState.ShowingChoices;
            StopIndicatorBounce();

            ClearSpawnedChoices();
            SetChoicesActive(true);

            string picked = null;
            string pickedText = null;
            if (options != null && _choiceButtonTemplate != null && _choiceContainer != null)
            {
                foreach (var option in options)
                {
                    if (option == null)
                        continue;

                    var value = option.Value;
                    var button = Instantiate(_choiceButtonTemplate, _choiceContainer);
                    button.gameObject.SetActive(true);

                    var label = button.GetComponentInChildren<TMP_Text>(true);
                    if (label != null)
                        label.text = option.Text;

                    var layoutElement = button.GetComponent<LayoutElement>();
                    if (layoutElement != null)
                    {
                        layoutElement.minHeight = _choiceButtonHeight;
                        layoutElement.preferredHeight = _choiceButtonHeight;
                    }

                    var optionText = option.Text;
                    button.onClick.AddListener(() =>
                    {
                        picked = value;
                        pickedText = optionText;
                    });
                    _spawnedChoices.Add(button.gameObject);
                }

                UpdateChoiceLayout(_spawnedChoices.Count);
            }

            if (_spawnedChoices.Count == 0)
            {
                Debug.LogWarning("[ConversationView] 表示できる選択肢がありません。");
                SetChoicesActive(false);
                onSelected?.Invoke(null);
                State = ConversationState.Entering;
                yield break;
            }

            yield return ShowAnimated();
            SelectFirstChoice();

            while (picked == null)
                yield return null;

            ClearSpawnedChoices();
            SetChoicesActive(false);
            EnsureHistoryPanel();
            _historyPanel?.AddChoiceEntry(pickedText);
            onSelected?.Invoke(picked);
            State = ConversationState.Entering;
        }

        /// <summary>
        /// 受け取ったアイテムの画像を表示し、送り入力まで待って片付ける。
        /// <paramref name="sprite"/> 未指定なら <see cref="_itemGetSprite"/>(ダミー)を使う。
        /// 画像は Prefab に要素を持たず実行時に Canvas 直下へ生成する(表示ロジックは常駐 UI 側に集約。
        /// 将来 EventPlayer の giveItem ステップから itemKey で解決した Sprite を渡す想定)。
        /// </summary>
        public IEnumerator ShowItemGet(Sprite sprite = null)
        {
            State = ConversationState.Entering;
            yield return ShowAnimated();
            SetChoicesActive(false);
            ShowItemGetImage(sprite != null ? sprite : _itemGetSprite);
            yield return WaitForAdvance();
            HideItemGet();
        }

        /// <summary>
        /// 受け取った武器の3Dモデルを RenderTexture 経由でUIに表示し、送り入力まで回転させて待つ。
        /// アイテム画像(<see cref="ShowItemGet"/>)と同じ位置・サイズに出す。カメラ/ライト/モデルのリグは
        /// シーンから離した場所へ実行時に組んで終了で破棄する(常駐なし・シーン非依存・専用レイヤー不要)。
        /// <paramref name="modelPrefab"/> 未指定なら <see cref="_weaponModelPrefab"/>(ダミー)。将来は
        /// EventPlayer の giveWeapon ステップから weaponKey で解決した Prefab を渡す想定。
        /// </summary>
        public IEnumerator ShowWeaponGet(GameObject modelPrefab = null)
        {
            State = ConversationState.Entering;
            yield return ShowAnimated();
            SetChoicesActive(false);

            var model = BuildWeaponRig(modelPrefab != null ? modelPrefab : _weaponModelPrefab);
            if (model == null)
            {
                yield return WaitForAdvance();
                yield break;
            }

            yield return WaitForAdvance(); // 回転させず静止表示。送り入力まで待つ
            HideWeaponGet();
        }

        // ---- 内部 ----

        private IEnumerator TypeBody(string text)
        {
            State = ConversationState.Typing;
            StopIndicatorBounce();
            if (_nextIndicator != null)
                _nextIndicator.SetActive(false);

            if (_bodyText == null)
                yield break;

            _bodyText.text = text;
            _bodyText.ForceMeshUpdate();
            int total = _bodyText.textInfo.characterCount;
            _bodyText.maxVisibleCharacters = 0;

            int shown = 0;
            while (shown < total)
            {
                if (AdvancePressed()) // 途中で送り入力 → 全文を即表示
                {
                    shown = total;
                    break;
                }
                shown++;
                _bodyText.maxVisibleCharacters = shown;

                float delay = Mathf.Max(0f, _charInterval);
                if (IsPunctuation(_bodyText.textInfo.characterInfo[shown - 1].character))
                    delay += Mathf.Max(0f, _punctuationDelay);
                if (FastForwardHeld())
                    delay *= Mathf.Clamp(_fastForwardMultiplier, 0.05f, 1f);

                if (delay > 0f)
                    yield return new WaitForSecondsRealtime(delay);
                else
                    yield return null;
            }
            _bodyText.maxVisibleCharacters = total;
        }

        private static bool IsPunctuation(char character) =>
            character is '。' or '、' or '！' or '？' or '!' or '?' or '…' or '．' or ',';

        private static bool FastForwardHeld()
        {
            var keyboard = Keyboard.current;
            if (
                keyboard != null
                && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)
            )
                return true;

            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.rightShoulder.isPressed;
        }

        private IEnumerator WaitForAdvance()
        {
            State = ConversationState.WaitingForAdvance;
            StartIndicatorBounce();
            yield return null; // タイプ送出と同フレームの入力を送りと二重に拾わない
            float autoElapsed = 0f;
            while (true)
            {
                if (_historyPanel != null && _historyPanel.IsOpen)
                {
                    autoElapsed = 0f;
                    yield return null;
                    continue;
                }

                if (AdvancePressed())
                    break;

                if (IsAutoMode)
                {
                    autoElapsed += Time.unscaledDeltaTime;
                    if (autoElapsed >= Mathf.Max(0.1f, _autoAdvanceDelay))
                        break;
                }
                else
                {
                    autoElapsed = 0f;
                }
                yield return null;
            }
            StopIndicatorBounce();
            State = ConversationState.Entering;
        }

        public void SetAutoMode(bool enabled)
        {
            if (IsAutoMode == enabled)
                return;

            IsAutoMode = enabled;
            EnsureAutoModeIndicator();
            RefreshAutoModeIndicator();
        }

        private static bool AdvancePressed()
        {
            var kb = Keyboard.current;
            if (
                kb != null
                && (
                    kb.spaceKey.wasPressedThisFrame
                    || kb.enterKey.wasPressedThisFrame
                    || kb.numpadEnterKey.wasPressedThisFrame
                    || kb.zKey.wasPressedThisFrame
                )
            )
                return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;

            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        }

        private readonly struct ResolvedPortrait
        {
            public ResolvedPortrait(
                Sprite sprite,
                Sprite icon,
                DialoguePortraitSide side,
                string displayName
            )
            {
                Sprite = sprite;
                Icon = icon;
                Side = side;
                DisplayName = displayName ?? string.Empty;
            }

            public Sprite Sprite { get; }
            public Sprite Icon { get; }
            public DialoguePortraitSide Side { get; }
            public string DisplayName { get; }
        }

        private ResolvedPortrait ResolvePortrait(string key)
        {
            if (!string.IsNullOrEmpty(key) && _characters != null)
            {
                foreach (var character in _characters)
                {
                    if (
                        character != null
                        && character.TryResolveVisual(key, out var portrait, out var icon)
                    )
                        return new ResolvedPortrait(
                            portrait,
                            icon,
                            character.Side,
                            character.DisplayName
                        );
                }
            }

            if (!string.IsNullOrEmpty(key) && _portraits != null)
            {
                foreach (var entry in _portraits)
                {
                    if (entry.Key == key && entry.Sprite != null)
                        return new ResolvedPortrait(
                            entry.Sprite,
                            entry.Sprite,
                            entry.Side,
                            string.Empty
                        );
                }
            }

            return new ResolvedPortrait(
                _defaultPortrait,
                _defaultPortrait,
                _defaultPortraitSide,
                string.Empty
            );
        }

        private IEnumerator SetPortrait(ResolvedPortrait resolved)
        {
            if (_portrait == null)
                yield break;

            EnsurePortraitSlots();

            var sprite = resolved.Sprite;
            var side = resolved.Side;

            if (sprite == null)
            {
                _portrait.enabled = false;
                if (_rightPortrait != null)
                    _rightPortrait.enabled = false;
                yield break;
            }

            var active = side == DialoguePortraitSide.Left ? _portrait : _rightPortrait;
            var inactive = side == DialoguePortraitSide.Left ? _rightPortrait : _portrait;
            bool wasShown =
                side == DialoguePortraitSide.Left ? _leftPortraitShown : _rightPortraitShown;
            bool isNewPortrait = !wasShown || active.sprite != sprite;

            active.sprite = sprite;
            active.enabled = true;
            ApplyPortraitSide(active, side);
            if (side == DialoguePortraitSide.Left)
                _leftPortraitShown = true;
            else
                _rightPortraitShown = true;

            Color activeStart = isNewPortrait ? new Color(1f, 1f, 1f, 0f) : active.color;
            float activeScaleStart = isNewPortrait
                ? _portraitInactiveScale
                : Mathf.Abs(active.rectTransform.localScale.x);
            Color inactiveStart = inactive != null ? inactive.color : Color.white;
            float inactiveScaleStart =
                inactive != null
                    ? Mathf.Abs(inactive.rectTransform.localScale.x)
                    : _portraitInactiveScale;
            float duration = isNewPortrait ? _portraitFadeDuration : _portraitFocusDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.01f, duration));
                active.color = Color.Lerp(activeStart, Color.white, t);
                SetPortraitScale(
                    active,
                    side,
                    Mathf.Lerp(activeScaleStart, _portraitActiveScale, t)
                );

                if (inactive != null && inactive.enabled)
                {
                    var inactiveTarget = new Color(
                        _portraitInactiveBrightness,
                        _portraitInactiveBrightness,
                        _portraitInactiveBrightness,
                        1f
                    );
                    inactive.color = Color.Lerp(inactiveStart, inactiveTarget, t);
                    SetPortraitScale(
                        inactive,
                        side == DialoguePortraitSide.Left
                            ? DialoguePortraitSide.Right
                            : DialoguePortraitSide.Left,
                        Mathf.Lerp(inactiveScaleStart, _portraitInactiveScale, t)
                    );
                }

                elapsed += Mathf.Max(Time.unscaledDeltaTime, 1f / 60f);
                yield return null;
            }

            active.color = Color.white;
            SetPortraitScale(active, side, _portraitActiveScale);
            if (inactive != null && inactive.enabled)
            {
                inactive.color = new Color(
                    _portraitInactiveBrightness,
                    _portraitInactiveBrightness,
                    _portraitInactiveBrightness,
                    1f
                );
                SetPortraitScale(
                    inactive,
                    side == DialoguePortraitSide.Left
                        ? DialoguePortraitSide.Right
                        : DialoguePortraitSide.Left,
                    _portraitInactiveScale
                );
            }
        }

        private void EnsurePortraitSlots()
        {
            if (_portrait == null)
                return;

            if (_rightPortrait != null)
                return;

            ApplyPortraitSide(_portrait, DialoguePortraitSide.Left);
            _rightPortrait = Instantiate(_portrait, _portrait.transform.parent);
            _rightPortrait.name = "PortraitRight";
            _rightPortrait.transform.SetSiblingIndex(_portrait.transform.GetSiblingIndex() + 1);
            _rightPortrait.enabled = false;
            ApplyPortraitSide(_rightPortrait, DialoguePortraitSide.Right);
        }

        private void ApplyPortraitSide(Image portrait, DialoguePortraitSide side)
        {
            var rect = portrait.rectTransform;
            float anchorX =
                side == DialoguePortraitSide.Left ? _portraitLeftAnchorX : _portraitRightAnchorX;
            rect.anchorMin = new Vector2(anchorX, rect.anchorMin.y);
            rect.anchorMax = new Vector2(anchorX, rect.anchorMax.y);
            rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
            SetPortraitScale(portrait, side, 1f);
        }

        private static void SetPortraitScale(
            Image portrait,
            DialoguePortraitSide side,
            float magnitude
        )
        {
            var rect = portrait.rectTransform;
            rect.localScale = new Vector3(
                (side == DialoguePortraitSide.Left ? 1f : -1f) * magnitude,
                magnitude,
                rect.localScale.z
            );
        }

        /// <summary>アイテム画像を Canvas 直下へ実行時生成する。既存表示があれば作り直す。</summary>
        private void ShowItemGetImage(Sprite icon)
        {
            HideItemGet();
            if (icon == null)
            {
                Debug.LogWarning(
                    "[ConversationView] 受け取りアイテムの Sprite が未設定です。_itemGetSprite を割り当ててください。"
                );
                return;
            }

            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas == null)
                return;

            _itemGetObject = new GameObject("ItemGetImage");
            // AddComponent<Image> が CanvasRenderer と RectTransform を自動付与する
            // (GameObject コンストラクタに typeof(Image) を渡すと CanvasRenderer が付かず不可視になる)。
            var img = _itemGetObject.AddComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = _itemGetPosition;
            rt.sizeDelta = _itemGetSize;
            rt.SetAsLastSibling(); // 立ち絵・ウィンドウ・テキストより手前に
        }

        private void HideItemGet()
        {
            if (_itemGetObject == null)
                return;
            Destroy(_itemGetObject);
            _itemGetObject = null;
        }

        /// <summary>
        /// 武器モデルのリグ(離れた場所のモデル+正射影カメラ+ポイントライト)と RenderTexture を実行時に組み、
        /// アイテム画像と同じ位置・サイズの RawImage で Canvas に出す。回すためモデルの GameObject を返す。
        /// </summary>
        private GameObject BuildWeaponRig(GameObject prefab)
        {
            HideWeaponGet();
            if (prefab == null)
            {
                Debug.LogWarning(
                    "[ConversationView] 武器モデルが未設定です。_weaponModelPrefab を割り当ててください。"
                );
                return null;
            }

            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas == null)
                return null;

            // シーンから離した場所にリグを組む(床/壁と前後せず、ライトもシーンに届かない)。
            _weaponRigObject = new GameObject("WeaponGetRig");
            _weaponRigObject.transform.position = new Vector3(0f, -10000f, 0f);

            var model = Instantiate(prefab, _weaponRigObject.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(_weaponModelEuler);

            if (!TryComputeBounds(model, out var bounds))
            {
                Debug.LogWarning("[ConversationView] 武器モデルに Renderer がなく表示できません。");
                HideWeaponGet();
                return null;
            }

            float radius = Mathf.Max(0.01f, bounds.extents.magnitude);
            float dist = radius * 2f + 1f;

            // Prefab に本フィールドが未保存だと Unity は初期値でなく (0,0) で読むため、
            // その場合は横長の既定(640×360)を使う(正方形の _itemGetSize に落とさない)。
            var boxSize =
                (_weaponImageSize.x > 1f && _weaponImageSize.y > 1f)
                    ? _weaponImageSize
                    : new Vector2(640f, 360f);
            float aspect = boxSize.x / Mathf.Max(1f, boxSize.y); // 枠の縦横比。RT・カメラ・フレーミングを全部これに合わせる

            // 正射影カメラ・透過背景。カメラは +Z を向く=画面X/YはワールドX/Y。
            var camObject = new GameObject("WeaponGetCamera");
            camObject.transform.SetParent(_weaponRigObject.transform, true);
            camObject.transform.position = bounds.center + Vector3.back * dist;
            camObject.transform.rotation = Quaternion.identity; // +Z(モデル)を向く
            var cam = camObject.AddComponent<Camera>();
            cam.orthographic = true;
            // 枠アスペクトに合わせ、縦(extents.y)と横(extents.x/aspect)の両方が収まるよう詰める × 余白率。
            float need = Mathf.Max(bounds.extents.y, bounds.extents.x / Mathf.Max(0.01f, aspect));
            float fill = _weaponFrameFill > 0.01f ? _weaponFrameFill : 1.3f; // 未保存(0)時の既定
            cam.orthographicSize = Mathf.Max(0.01f, need) * Mathf.Clamp(fill, 0.6f, 3f);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = dist + radius * 2f + 1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // 透過(UIに重ねる)
            cam.allowHDR = false;
            cam.allowMSAA = false;

            // URP は既定だと RenderTexture の背景を不透明(黒)に焼くため、ポスト処理等を明示的に切って
            // クリア色のアルファ0を透過として残す(これが無いと黒い正方形になる)。
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = false;
            camData.antialiasing = AntialiasingMode.None;
            camData.renderShadows = false;

            // キー(カメラ側=見える面)+ フィル(反対側の影を弱める)の2灯。小範囲なのでシーンには届かない。
            // 見える面をしっかり照らして、暗い背景に重なっても沈まないようにする。
            AddRigLight(
                bounds.center + new Vector3(radius, radius * 1.5f, -dist),
                radius * 20f,
                4f
            );
            AddRigLight(
                bounds.center + new Vector3(-radius, radius * 0.5f, -dist * 0.5f),
                radius * 20f,
                1.5f
            );

            // RT も枠と同じアスペクトにして引き伸ばし歪みを防ぐ(_weaponTextureSize は高さ基準)。
            int rtH = Mathf.Clamp(_weaponTextureSize, 64, 2048);
            int rtW = Mathf.Clamp(Mathf.RoundToInt(rtH * aspect), 64, 2048);
            _weaponRt = new RenderTexture(rtW, rtH, 16, RenderTextureFormat.ARGB32);
            _weaponRt.Create();
            cam.targetTexture = _weaponRt;

            // 背後の枠パネル(枠サイズ=boxSize に連動＝横長)。未保存時は (0,0,0,0) で読まれるので、
            // その場合も既定の半透明ダークで出す(枠が見えないと横長か判別できないため)。
            var backdropColor =
                _weaponBackdropColor.a > 0.001f
                    ? _weaponBackdropColor
                    : new Color(0f, 0f, 0f, 0.35f);
            {
                _weaponBackdropObject = new GameObject("WeaponGetBackdrop");
                var bg = _weaponBackdropObject.AddComponent<Image>();
                bg.color = backdropColor;
                bg.raycastTarget = false;
                var brt = bg.rectTransform;
                brt.SetParent(canvas.transform, false);
                brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
                brt.anchoredPosition = _itemGetPosition;
                brt.sizeDelta = boxSize;
                brt.SetAsLastSibling();
            }

            _weaponRawImageObject = new GameObject("WeaponGetImage");
            var raw = _weaponRawImageObject.AddComponent<RawImage>();
            raw.texture = _weaponRt;
            raw.raycastTarget = false;
            var rt = raw.rectTransform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = _itemGetPosition; // アイテム画像と同じ場所
            rt.sizeDelta = boxSize;
            rt.SetAsLastSibling(); // 地色パネル・立ち絵・ウィンドウ・テキストより手前に

            return model;
        }

        private void AddRigLight(Vector3 position, float range, float intensity)
        {
            var go = new GameObject("WeaponGetLight");
            go.transform.SetParent(_weaponRigObject.transform, true);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
        }

        private void HideWeaponGet()
        {
            if (_weaponRawImageObject != null)
            {
                Destroy(_weaponRawImageObject);
                _weaponRawImageObject = null;
            }
            if (_weaponBackdropObject != null)
            {
                Destroy(_weaponBackdropObject);
                _weaponBackdropObject = null;
            }
            if (_weaponRigObject != null)
            {
                Destroy(_weaponRigObject);
                _weaponRigObject = null;
            }
            if (_weaponRt != null)
            {
                _weaponRt.Release();
                Destroy(_weaponRt);
                _weaponRt = null;
            }
        }

        private static bool TryComputeBounds(GameObject go, out Bounds bounds)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        private void StartIndicatorBounce()
        {
            StopIndicatorBounce();
            if (_nextIndicator == null)
                return;

            var rect = _nextIndicator.transform as RectTransform;
            if (rect == null)
                return;

            _indicatorBasePosition = rect.anchoredPosition;
            _hasIndicatorBasePosition = true;
            _nextIndicator.SetActive(true);
            _indicatorBounce = StartCoroutine(IndicatorBounceRoutine(rect));
        }

        private void StopIndicatorBounce()
        {
            if (_indicatorBounce != null)
            {
                StopCoroutine(_indicatorBounce);
                _indicatorBounce = null;
            }

            if (
                _hasIndicatorBasePosition
                && _nextIndicator != null
                && _nextIndicator.transform is RectTransform rect
            )
                rect.anchoredPosition = _indicatorBasePosition;

            _hasIndicatorBasePosition = false;
            if (_nextIndicator != null)
                _nextIndicator.SetActive(false);
        }

        private IEnumerator IndicatorBounceRoutine(RectTransform rect)
        {
            float elapsed = 0f;
            while (true)
            {
                float offset = CalculateIndicatorBounceOffset(elapsed);
                rect.anchoredPosition = _indicatorBasePosition + Vector2.up * offset;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private float CalculateIndicatorBounceOffset(float elapsed)
        {
            float duration = Mathf.Max(0.01f, _indicatorBounceDuration);
            float phase = Mathf.Repeat(elapsed / duration, 1f);
            return Mathf.Sin(phase * Mathf.PI) * _indicatorBounceHeight;
        }

        private void SetChoicesActive(bool active)
        {
            if (_choiceContainer != null)
                _choiceContainer.gameObject.SetActive(active);
        }

        private void UpdateChoiceLayout(int choiceCount)
        {
            if (_choiceContainer == null || choiceCount <= 0)
                return;

            float height = choiceCount * _choiceButtonHeight + (choiceCount - 1) * _choiceSpacing;

            var layoutGroup = _choiceContainer.GetComponent<VerticalLayoutGroup>();
            if (layoutGroup != null)
                layoutGroup.spacing = _choiceSpacing;

            // ウィンドウの上端を基準にすることで、2択・3択とも本文や名前欄へ重ならない。
            _choiceContainer.anchorMin = new Vector2(0.5f, 1f);
            _choiceContainer.anchorMax = new Vector2(0.5f, 1f);
            _choiceContainer.pivot = new Vector2(0.5f, 0f);
            _choiceContainer.anchoredPosition = new Vector2(0f, _choiceBottomMargin);
            _choiceContainer.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                _choiceContainerWidth
            );
            _choiceContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private void ClearSpawnedChoices()
        {
            foreach (var go in _spawnedChoices)
            {
                if (go != null)
                    Destroy(go);
            }
            _spawnedChoices.Clear();
        }

        private void SelectFirstChoice()
        {
            if (_spawnedChoices.Count == 0 || EventSystem.current == null)
                return;

            var firstButton = _spawnedChoices[0].GetComponent<Button>();
            if (firstButton != null && firstButton.IsInteractable())
                firstButton.Select();
        }

        private void EnsureAutoModeIndicator()
        {
            if (_autoModeIndicator != null || _root == null)
                return;

            var indicatorObject = new GameObject("AutoModeIndicator", typeof(RectTransform));
            indicatorObject.transform.SetParent(_root.transform, false);
            indicatorObject.transform.SetAsLastSibling();

            var rect = indicatorObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-36f, -28f);
            rect.sizeDelta = new Vector2(180f, 52f);

            _autoModeIndicator = indicatorObject.AddComponent<TextMeshProUGUI>();
            _autoModeIndicator.alignment = TextAlignmentOptions.Center;
            _autoModeIndicator.fontSize = 28f;
            _autoModeIndicator.fontStyle = FontStyles.Bold;
            _autoModeIndicator.color = new Color(0.75f, 0.9f, 1f, 1f);
            _autoModeIndicator.raycastTarget = false;
            if (_nameText != null)
                _autoModeIndicator.font = _nameText.font;
        }

        private void EnsureHistoryPanel()
        {
            if (_historyPanel == null)
                _historyPanel = GetComponent<DialogueHistoryPanel>();
            if (_historyPanel == null)
                _historyPanel = gameObject.AddComponent<DialogueHistoryPanel>();

            _historyPanel.Initialize(_nameText != null ? _nameText.font : null);
        }

        private void RefreshAutoModeIndicator()
        {
            if (_autoModeIndicator == null)
                return;

            _autoModeIndicator.text = _autoModeLabel;
            _autoModeIndicator.gameObject.SetActive(IsAutoMode);
        }

        private IEnumerator ShowAnimated()
        {
            if (_root == null)
                yield break;

            _root.interactable = false;
            _root.blocksRaycasts = true;

            if (_root.alpha >= 0.999f)
            {
                _root.interactable = true;
                yield break;
            }

            if (_windowRoot != null && !_hasWindowBasePosition)
            {
                _windowBasePosition = _windowRoot.anchoredPosition;
                _hasWindowBasePosition = true;
            }

            float duration = Mathf.Max(0.01f, _windowEnterDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                _root.alpha = t;
                if (_windowRoot != null && _hasWindowBasePosition)
                    _windowRoot.anchoredPosition =
                        _windowBasePosition + Vector2.up * Mathf.Lerp(_windowEnterOffsetY, 0f, t);

                elapsed += Mathf.Max(Time.unscaledDeltaTime, 1f / 60f);
                yield return null;
            }

            _root.alpha = 1f;
            _root.interactable = true;
            if (_windowRoot != null && _hasWindowBasePosition)
                _windowRoot.anchoredPosition = _windowBasePosition;
        }

        private void HideImmediate()
        {
            State = ConversationState.Exiting;
            StopIndicatorBounce();
            HideItemGet();
            HideWeaponGet();
            SetChoicesActive(false);
            if (_root != null)
            {
                _root.alpha = 0f;
                _root.interactable = false;
                _root.blocksRaycasts = false;
            }
            if (_windowRoot != null && _hasWindowBasePosition)
                _windowRoot.anchoredPosition = _windowBasePosition;
            State = ConversationState.Hidden;
        }
    }
}
