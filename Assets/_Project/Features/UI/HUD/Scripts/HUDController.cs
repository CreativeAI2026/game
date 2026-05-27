using UnityEngine;
using UnityEngine.UI;
using CreativeAI.UI.Common;

namespace CreativeAI.UI.HUD
{
    /// <summary>
    /// Field シーン上の常駐 HUD。右上の Character / Inventory / Save ボタンから各パネルを直接開く。
    /// 後でアイコンスプライトに差し替え予定。
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Buttons (top-right icons)")]
        [SerializeField] private Button _characterButton;
        [SerializeField] private Button _inventoryButton;
        [SerializeField] private Button _saveButton;

        [Header("Panels")]
        [SerializeField] private UIPanelStub _characterPanel;
        [SerializeField] private UIPanelStub _inventoryPanel;
        [SerializeField] private UIPanelStub _savePanel;

        private void Awake()
        {
            _characterButton?.onClick.AddListener(() => _characterPanel?.Open());
            _inventoryButton?.onClick.AddListener(() => _inventoryPanel?.Open());
            _saveButton?.onClick.AddListener(() => _savePanel?.Open());
        }
    }
}
