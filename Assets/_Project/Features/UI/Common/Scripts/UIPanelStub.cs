using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.Common
{
    /// <summary>
    /// 汎用パネル基底。Open() / Close() で SetActive を切り替える。
    /// 戻るボタン (_closeButton) があれば Awake で自動的に Close に配線する。
    /// </summary>
    public class UIPanelStub : MonoBehaviour
    {
        [SerializeField]
        private Button _closeButton;

        protected virtual void Awake()
        {
            if (_closeButton != null)
            {
                UIButtonHoverScaleUtility.ApplyTo(_closeButton);
                _closeButton.onClick.AddListener(Close);
            }
        }

        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
