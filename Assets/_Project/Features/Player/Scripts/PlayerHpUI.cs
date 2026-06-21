using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Gameplay
{
    public class PlayerHpUI : MonoBehaviour
    {
        [Header("HPバー")]
        // 最大HPを表示するテキスト
        [Tooltip("最大HPを表示するテキスト")]
        [SerializeField]
        private TextMeshProUGUI _maxHpText;

        // 現在のHPを表示するテキスト
        [Tooltip("現在のHPを表示するテキスト")]
        [SerializeField]
        private TextMeshProUGUI _currentHpText;

        // HPバー
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

        // プレイヤーのHPが変わった際に呼ばれる
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
