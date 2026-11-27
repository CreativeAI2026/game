using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// タイトルの「はじめから / 続きから」。上下キー・十字キー・スティックで選び、決定キー・決定ボタンで押す。
    /// 選んでいる項目は色と大きさで分かるようにする(文字だけのボタンなので、Button の色替えでは見えない)。
    /// 続きから は、セーブが無ければ暗くして選べない。
    /// </summary>
    public sealed class TitleUIController : MonoBehaviour
    {
        [SerializeField]
        private Button _tapToStartButton; // 「はじめから」(新規開始)

        [SerializeField]
        private Button _continueButton; // 「続きから」(セーブ復元)。未割当なら続きから導線は無効

        [SerializeField]
        private string _nextSceneName = SceneNames.FieldArea01;

        [Header("選択の見た目")]
        [SerializeField]
        private Color _selectedColor = new Color(1f, 0.88f, 0.45f, 1f);

        [SerializeField]
        private Color _disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);

        [SerializeField, Min(1f)]
        private float _selectedScale = 1.15f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("スティックを倒したとみなす深さ")]
        private float _stickThreshold = 0.6f;

        // 画面の上から順。項目を足すときはここへ並べる。
        private Button[] _items;
        private Color[] _normalColors;
        private Canvas[] _canvases; // 項目が載る Canvas。マウス位置の判定に、そのカメラを使う
        private bool[] _available; // 選べる項目か(押した後は Button を塞ぐので、暗くするかはこちらで決める)
        private int _selected;
        private bool _started;
        private bool _stickWasDown;
        private bool _stickWasUp;

        private void Awake()
        {
            if (_tapToStartButton == null)
            {
                Debug.LogError("[TitleUIController] _tapToStartButton is not assigned.");
                return;
            }
            _tapToStartButton.onClick.AddListener(OnTapToStart);

            // 続きから: セーブが在るときだけ押せる。ボタン未割当なら何もしない(導線なし)。
            if (_continueButton != null)
            {
                _continueButton.onClick.AddListener(OnContinue);
                _continueButton.interactable = SaveService.HasSave();
            }

            _items =
                _continueButton != null
                    ? new[] { _tapToStartButton, _continueButton }
                    : new[] { _tapToStartButton };
            _normalColors = new Color[_items.Length];
            _available = new bool[_items.Length];
            _canvases = new Canvas[_items.Length];
            for (int i = 0; i < _items.Length; i++)
            {
                _normalColors[i] = _items[i].targetGraphic.color;
                _available[i] = _items[i].interactable;
                _canvases[i] = _items[i].GetComponentInParent<Canvas>();
            }

            // 続きから があれば最初はそちらを選んでおく。
            _selected = _available[^1] ? _items.Length - 1 : 0;
            Refresh();
        }

        private void Update()
        {
            if (_items == null || _started)
                return;

            int step = ReadMoveStep();
            if (step != 0)
                Select(TitleMenuSelection.Move(_available, _selected, step));

            SelectByMouse();

            if (WasSubmitPressed() && _available[_selected])
                _items[_selected].onClick.Invoke();
        }

        private void Select(int index)
        {
            if (index < 0 || index == _selected)
                return;
            _selected = index;
            Refresh();
        }

        /// <summary>マウスを動かしたときだけ、乗っている項目へ選択を移す(動かさない間はキーの選択を邪魔しない)。</summary>
        private void SelectByMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null || mouse.delta.ReadValue() == Vector2.zero)
                return;

            for (int i = 0; i < _items.Length; i++)
            {
                if (!_available[i])
                    continue;
                var rect = (RectTransform)_items[i].transform;
                var camera = _canvases[i] != null ? _canvases[i].worldCamera : null;
                if (
                    RectTransformUtility.RectangleContainsScreenPoint(
                        rect,
                        mouse.position.ReadValue(),
                        camera
                    )
                )
                {
                    Select(i);
                    return;
                }
            }
        }

        /// <summary>この瞬間に押された上下の入力。下が +1、上が -1、無ければ 0。</summary>
        private int ReadMoveStep()
        {
            int step = 0;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                    step += 1;
                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                    step -= 1;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                if (gamepad.dpad.down.wasPressedThisFrame)
                    step += 1;
                if (gamepad.dpad.up.wasPressedThisFrame)
                    step -= 1;

                // スティックは倒した瞬間だけ1回動かす(倒しっぱなしで走らせない)。
                float y = gamepad.leftStick.y.ReadValue();
                bool down = y <= -_stickThreshold;
                bool up = y >= _stickThreshold;
                if (down && !_stickWasDown)
                    step += 1;
                if (up && !_stickWasUp)
                    step -= 1;
                _stickWasDown = down;
                _stickWasUp = up;
            }
            return Mathf.Clamp(step, -1, 1);
        }

        private static bool WasSubmitPressed()
        {
            var keyboard = Keyboard.current;
            if (
                keyboard != null
                && (
                    keyboard.enterKey.wasPressedThisFrame
                    || keyboard.numpadEnterKey.wasPressedThisFrame
                    || keyboard.spaceKey.wasPressedThisFrame
                )
            )
                return true;

            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        }

        /// <summary>選択中は色と大きさを変え、押せない項目は暗くする。</summary>
        private void Refresh()
        {
            for (int i = 0; i < _items.Length; i++)
            {
                var item = _items[i];
                bool selected = i == _selected && _available[i];
                item.targetGraphic.color =
                    !_available[i] ? _disabledColor
                    : selected ? _selectedColor
                    : _normalColors[i];
                item.transform.localScale = Vector3.one * (selected ? _selectedScale : 1f);
            }
        }

        private void OnDestroy()
        {
            if (_tapToStartButton != null)
            {
                _tapToStartButton.onClick.RemoveListener(OnTapToStart);
            }
            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveListener(OnContinue);
            }
        }

        /// <summary>「はじめから」: はじめから遊ぶ(中身は Session 層の <see cref="IGameStarter"/>)。</summary>
        private void OnTapToStart()
        {
            if (!TryGetStarter(out var starter) || !starter.StartNewGame(_nextSceneName))
                return;
            Lock();
        }

        /// <summary>「続きから」: セーブから続ける。セーブが壊れていて読めなければ、押しても毎回失敗するのでボタンを塞ぐ。</summary>
        private void OnContinue()
        {
            if (!TryGetStarter(out var starter))
                return;
            var started = starter.ContinueGame(_nextSceneName);
            if (started)
            {
                Lock();
                return;
            }

            // 読めなかった: 以後は選べない項目として暗くし、選択を「はじめから」へ移す。
            if (_continueButton != null)
            {
                _continueButton.interactable = false;
                _available[1] = false;
            }
            Select(TitleMenuSelection.Move(_available, _selected, 1));
            Refresh();
        }

        /// <summary>読み込みが始まったら、どちらも押せなくして選択の見た目も止める。</summary>
        private void Lock()
        {
            _started = true;
            foreach (var item in _items)
                item.interactable = false;
            Refresh();
        }

        private static bool TryGetStarter(out IGameStarter starter)
        {
            starter = GameStartService.Current;
            if (starter == null)
                Debug.LogError(
                    "[TitleUIController] GameStartService.Current が登録されていません。"
                );
            return starter != null;
        }
    }
}
