using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// PlayerStatusのHP変動イベントを購読し、UIスライダーとテキストに反映する。
    /// </summary>
    public class PlayerHpUI : MonoBehaviour
    {
        [Header("HPバー")]
        [Tooltip("最大HPを表示するテキスト")]
        [SerializeField]
        private TextMeshProUGUI _maxHpText;

        [Tooltip("現在のHPを表示するテキスト")]
        [SerializeField]
        private TextMeshProUGUI _currentHpText;

        [Tooltip("HPバー")]
        [SerializeField]
        private Slider _hpSlider;

        [Header("参照")]
        [Tooltip("プレイヤーのステータス")]
        [SerializeField]
        private PlayerStatus _playerStatus;

        void OnEnable()
        {
            if (_playerStatus != null)
            {
                _playerStatus.OnHpChanged += UpdateUI;
            }
        }

        private void OnDisable()
        {
            if (_playerStatus != null)
            {
                _playerStatus.OnHpChanged -= UpdateUI;
            }
        }

        private void UpdateUI(float currentHp, float maxHp)
        {
            _hpSlider.maxValue = maxHp;
            _hpSlider.value = currentHp;

            if (_currentHpText != null)
            {
                _currentHpText.text = currentHp.ToString("0");
            }

            if (_maxHpText != null)
            {
                _maxHpText.text = maxHp.ToString("0");
            }
        }
    }
}
