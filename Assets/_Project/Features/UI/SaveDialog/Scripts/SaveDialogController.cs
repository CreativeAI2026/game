using UnityEngine;
using UnityEngine.UI;
using CreativeAI.UI.Common;

namespace CreativeAI.UI.SaveDialog
{
    public class SaveDialogController : UIPanelStub
    {
        [SerializeField] private Button _yesButton;
        [SerializeField] private Button _noButton;

        protected override void Awake()
        {
            base.Awake();
            if (_yesButton != null) _yesButton.onClick.AddListener(OnYes);
            if (_noButton != null) _noButton.onClick.AddListener(OnNo);
        }

        private void OnDestroy()
        {
            if (_yesButton != null) _yesButton.onClick.RemoveAllListeners();
            if (_noButton != null) _noButton.onClick.RemoveAllListeners();
        }

        private void OnYes()
        {
            // 実セーブ処理は別途実装予定。今はログのみ
            Debug.Log("[SaveDialog] セーブ実行（仮）");
            Close();
        }

        private void OnNo()
        {
            Close();
        }
    }
}
