using CreativeAI.Core;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public class TitleUIController : MonoBehaviour
    {
        [SerializeField]
        private Button _tapToStartButton; // 「はじめる」(新規開始)

        [SerializeField]
        private Button _continueButton; // 「続きから」(セーブ復元)。未割当なら続きから導線は無効

        [SerializeField]
        private string _nextSceneName = SceneNames.FieldArea01;

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
        }

        private void Start()
        {
            SelectDefaultButton();
        }

        private void Update()
        {
            if (
                EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == null
            )
            {
                SelectDefaultButton();
            }
        }

        private void SelectDefaultButton()
        {
            if (EventSystem.current == null)
                return;

            Button defaultButton =
                _continueButton != null && _continueButton.interactable
                    ? _continueButton
                    : _tapToStartButton;
            if (defaultButton != null && defaultButton.interactable)
            {
                EventSystem.current.SetSelectedGameObject(defaultButton.gameObject);
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

        /// <summary>「はじめる」: はじめから遊ぶ(中身は Session 層の <see cref="IGameStarter"/>)。</summary>
        private void OnTapToStart()
        {
            if (!TryGetStarter(out var starter) || !starter.StartNewGame(_nextSceneName))
                return;
            _tapToStartButton.interactable = false;
        }

        /// <summary>「続きから」: セーブから続ける。セーブが壊れていて読めなければ、押しても毎回失敗するのでボタンを塞ぐ。</summary>
        private void OnContinue()
        {
            if (!TryGetStarter(out var starter))
                return;
            var started = starter.ContinueGame(_nextSceneName);
            if (_continueButton != null)
                _continueButton.interactable = false;
            if (started)
                _tapToStartButton.interactable = false;
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
