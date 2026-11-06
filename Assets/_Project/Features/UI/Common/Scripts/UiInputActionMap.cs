using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// <see cref="EventSystem"/> の <see cref="InputSystemUIInputModule"/> に紐づく <c>UI</c> アクションマップから
    /// UI 入力（移動・決定・キャンセル・タブ切替・会話ショートカット等）を取得する。
    /// 各 UI クラスが物理デバイス（Keyboard / Gamepad / Mouse）を直接参照せず、InputSystem と EventSystem ベースで入力を扱うための窓口。
    /// </summary>
    public static class UiInputActionMap
    {
        public const string UiMapName = "UI";
        public const string Navigate = "Navigate";
        public const string Submit = "Submit";
        public const string Cancel = "Cancel";
        public const string Point = "Point";
        public const string Click = "Click";
        public const string TabPrev = "TabPrev";
        public const string TabNext = "TabNext";
        public const string SubTabPrev = "SubTabPrev";
        public const string SubTabNext = "SubTabNext";
        public const string AuxAction = "AuxAction";
        public const string CraftExecute = "CraftExecute";
        public const string DialogueAdvance = "DialogueAdvance";
        public const string DialogueFastForward = "DialogueFastForward";
        public const string DialogueSkip = "DialogueSkip";
        public const string DialogueAuto = "DialogueAuto";
        public const string DialogueHistory = "DialogueHistory";
        public const string DialogueHide = "DialogueHide";
        public const string DialogueSpeed = "DialogueSpeed";

        private const float MoveThreshold = 0.5f;
        private const float DefaultRepeatDelay = 0.5f;
        private const float DefaultRepeatRate = 0.1f;

        private static InputActionMap _fallbackMap;
        private static int _lastMoveFrame = -1;
        private static Vector2Int _lastMoveDirection;
        private static Vector2Int _frameDiscreteStep;
        private static float _nextRepeatTime;

        public static bool WasPressedThisFrame(string actionName)
        {
            var action = ResolveAction(actionName);
            return action != null && action.WasPressedThisFrame();
        }

        public static bool IsPressed(string actionName)
        {
            var action = ResolveAction(actionName);
            return action != null && action.IsPressed();
        }

        public static bool WasSubmitPressedThisFrame()
        {
            var module = GetUiInputModule();
            var submitAction =
                module != null && module.submit != null ? module.submit.action : null;
            submitAction ??= ResolveAction(Submit);
            return submitAction != null && submitAction.WasPressedThisFrame();
        }

        public static bool WasCancelPressedThisFrame()
        {
            var module = GetUiInputModule();
            var cancelAction =
                module != null && module.cancel != null ? module.cancel.action : null;
            cancelAction ??= ResolveAction(Cancel);
            return cancelAction != null && cancelAction.WasPressedThisFrame();
        }

        public static bool WasClickPressedThisFrame()
        {
            var module = GetUiInputModule();
            var clickAction =
                module != null && module.leftClick != null ? module.leftClick.action : null;
            clickAction ??= ResolveAction(Click);
            return clickAction != null && clickAction.WasPressedThisFrame();
        }

        public static bool IsClickPressed()
        {
            var module = GetUiInputModule();
            var clickAction =
                module != null && module.leftClick != null ? module.leftClick.action : null;
            clickAction ??= ResolveAction(Click);
            return clickAction != null && clickAction.IsPressed();
        }

        public static bool TryReadPointerPosition(out Vector2 position)
        {
            var module = GetUiInputModule();
            var pointAction = module != null && module.point != null ? module.point.action : null;
            pointAction ??= ResolveAction(Point);
            if (pointAction != null)
            {
                position = pointAction.ReadValue<Vector2>();
                return true;
            }

            position = Vector2.zero;
            return false;
        }

        public static Vector2 ReadNavigate()
        {
            var module = GetUiInputModule();
            var moveAction = module != null && module.move != null ? module.move.action : null;
            moveAction ??= ResolveAction(Navigate);
            return moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        }

        /// <summary>
        /// <c>UI/Navigate</c> アクションから、グリッド・リスト移動用の離散ステップ（押下瞬間＋長押しリピート）を取得する。
        /// </summary>
        public static bool TryReadDiscreteMove(out Vector2Int step)
        {
            UpdateDiscreteMoveForCurrentFrame();
            step = _frameDiscreteStep;
            return step != Vector2Int.zero;
        }

        private static void UpdateDiscreteMoveForCurrentFrame()
        {
            if (_lastMoveFrame == Time.frameCount)
                return;

            _lastMoveFrame = Time.frameCount;
            Vector2 raw = ReadNavigate();
            int x =
                raw.x <= -MoveThreshold ? -1
                : raw.x >= MoveThreshold ? 1
                : 0;
            int y =
                raw.y <= -MoveThreshold ? -1
                : raw.y >= MoveThreshold ? 1
                : 0;

            Vector2Int direction =
                x != 0 && y != 0
                    ? (
                        Mathf.Abs(raw.x) >= Mathf.Abs(raw.y)
                            ? new Vector2Int(x, 0)
                            : new Vector2Int(0, y)
                    )
                    : new Vector2Int(x, y);

            if (direction == Vector2Int.zero)
            {
                _lastMoveDirection = Vector2Int.zero;
                _frameDiscreteStep = Vector2Int.zero;
                return;
            }

            var module = GetUiInputModule();
            float repeatDelay = module != null ? module.moveRepeatDelay : DefaultRepeatDelay;
            float repeatRate = module != null ? module.moveRepeatRate : DefaultRepeatRate;

            if (direction != _lastMoveDirection)
            {
                _lastMoveDirection = direction;
                _nextRepeatTime = Time.unscaledTime + Mathf.Max(0.05f, repeatDelay);
                _frameDiscreteStep = direction;
                return;
            }

            if (Time.unscaledTime >= _nextRepeatTime)
            {
                _nextRepeatTime = Time.unscaledTime + Mathf.Max(0.02f, repeatRate);
                _frameDiscreteStep = direction;
                return;
            }

            _frameDiscreteStep = Vector2Int.zero;
        }

        private static InputSystemUIInputModule GetUiInputModule()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return null;

            if (eventSystem.currentInputModule is InputSystemUIInputModule currentModule)
                return currentModule;

            return eventSystem.GetComponent<InputSystemUIInputModule>();
        }

        private static InputAction ResolveAction(string actionName)
        {
            var module = GetUiInputModule();
            if (module != null && module.actionsAsset != null)
            {
                var uiMap = module.actionsAsset.FindActionMap(UiMapName, false);
                if (uiMap != null)
                {
                    if (!uiMap.enabled)
                        uiMap.Enable();
                    var action = uiMap.FindAction(actionName, false);
                    if (action != null)
                        return action;
                }
            }

            var player = PlayerRegistry.Current;
            if (
                player != null
                && player.TryGetComponent<PlayerInput>(out var playerInput)
                && playerInput.actions != null
            )
            {
                var uiMap = playerInput.actions.FindActionMap(UiMapName, false);
                var action = uiMap?.FindAction(actionName, false);
                if (action != null)
                {
                    if (!action.enabled)
                        action.Enable();
                    return action;
                }
            }

            return GetOrCreateFallbackMap().FindAction(actionName, false);
        }

        private static InputActionMap GetOrCreateFallbackMap()
        {
            if (_fallbackMap != null)
            {
                if (!_fallbackMap.enabled)
                    _fallbackMap.Enable();
                return _fallbackMap;
            }

            _fallbackMap = new InputActionMap(UiMapName);

            var nav = _fallbackMap.AddAction(Navigate, InputActionType.PassThrough);
            nav.AddBinding("<Gamepad>/dpad");
            nav.AddBinding("<Gamepad>/leftStick");
            nav.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/s")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/a")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/d")
                .With("Right", "<Keyboard>/rightArrow");

            var submit = _fallbackMap.AddAction(Submit, InputActionType.Button);
            submit.AddBinding("<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/numpadEnter");
            submit.AddBinding("<Keyboard>/space");
            submit.AddBinding("<Gamepad>/buttonEast");

            var cancel = _fallbackMap.AddAction(Cancel, InputActionType.Button);
            cancel.AddBinding("<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/buttonSouth");

            var point = _fallbackMap.AddAction(Point, InputActionType.PassThrough);
            point.AddBinding("<Mouse>/position");
            point.AddBinding("<Pen>/position");
            point.AddBinding("<Touchscreen>/touch*/position");

            var click = _fallbackMap.AddAction(Click, InputActionType.PassThrough);
            click.AddBinding("<Mouse>/leftButton");
            click.AddBinding("<Pen>/tip");
            click.AddBinding("<Touchscreen>/touch*/press");

            _fallbackMap
                .AddAction(TabPrev, InputActionType.Button)
                .AddBinding("<Gamepad>/leftShoulder");
            _fallbackMap
                .AddAction(TabNext, InputActionType.Button)
                .AddBinding("<Gamepad>/rightShoulder");
            _fallbackMap
                .AddAction(SubTabPrev, InputActionType.Button)
                .AddBinding("<Gamepad>/leftTrigger");
            _fallbackMap
                .AddAction(SubTabNext, InputActionType.Button)
                .AddBinding("<Gamepad>/rightTrigger");

            var aux = _fallbackMap.AddAction(AuxAction, InputActionType.Button);
            aux.AddBinding("<Gamepad>/buttonNorth");
            aux.AddBinding("<Keyboard>/x");

            _fallbackMap
                .AddAction(CraftExecute, InputActionType.Button)
                .AddBinding("<Gamepad>/buttonWest");

            var advance = _fallbackMap.AddAction(DialogueAdvance, InputActionType.Button);
            advance.AddBinding("<Keyboard>/space");
            advance.AddBinding("<Keyboard>/enter");
            advance.AddBinding("<Keyboard>/numpadEnter");
            advance.AddBinding("<Keyboard>/f");
            advance.AddBinding("<Keyboard>/z");
            advance.AddBinding("<Gamepad>/buttonEast");

            var fastForward = _fallbackMap.AddAction(DialogueFastForward, InputActionType.Button);
            fastForward.AddBinding("<Keyboard>/leftCtrl");
            fastForward.AddBinding("<Keyboard>/rightCtrl");
            fastForward.AddBinding("<Keyboard>/f");
            fastForward.AddBinding("<Gamepad>/rightShoulder");

            var skip = _fallbackMap.AddAction(DialogueSkip, InputActionType.Button);
            skip.AddBinding("<Keyboard>/s");
            skip.AddBinding("<Gamepad>/rightTrigger");

            var auto = _fallbackMap.AddAction(DialogueAuto, InputActionType.Button);
            auto.AddBinding("<Keyboard>/a");
            auto.AddBinding("<Gamepad>/buttonWest");

            var history = _fallbackMap.AddAction(DialogueHistory, InputActionType.Button);
            history.AddBinding("<Keyboard>/l");
            history.AddBinding("<Gamepad>/buttonNorth");

            var hide = _fallbackMap.AddAction(DialogueHide, InputActionType.Button);
            hide.AddBinding("<Keyboard>/h");
            hide.AddBinding("<Gamepad>/buttonSouth");

            var speed = _fallbackMap.AddAction(DialogueSpeed, InputActionType.Button);
            speed.AddBinding("<Keyboard>/t");
            speed.AddBinding("<Gamepad>/leftShoulder");

            _fallbackMap.Enable();
            return _fallbackMap;
        }
    }
}
