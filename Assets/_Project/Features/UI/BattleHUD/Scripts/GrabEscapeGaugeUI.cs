using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Gameplay
{
    public class GrabEscapeGaugeUI : MonoBehaviour
    {
        public static GrabEscapeGaugeUI Instance { get; private set; }

        [Header("円形ゲージ")]
        [SerializeField]
        private Image _gaugeImage;

        [SerializeField]
        private Image _gaugeBgImage;

        [Header("ラベル")]
        [SerializeField]
        private TextMeshProUGUI _labelText;

        [Header("色設定")]
        [SerializeField]
        private Color _colorLow = new Color(0.2f, 0.8f, 1f, 1f);

        [SerializeField]
        private Color _colorHigh = new Color(1f, 0.9f, 0.1f, 1f);

        [Header("点滅設定")]
        [SerializeField]
        private float _blinkFrequency = 3f;

        [SerializeField]
        [Range(0f, 1f)]
        private float _blinkAlphaMin = 0.4f;

        private CanvasGroup _canvasGroup;
        private bool _isVisible = false;
        private float _currentValue = 0f;
        private float _maxValue = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            SetVisible(false);

            // イベントの購読
            GrabEscapeEvents.OnShowGauge += Show;
            GrabEscapeEvents.OnUpdateGauge += UpdateValue;
            GrabEscapeEvents.OnHideGauge += Hide;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // イベントの購読解除
            GrabEscapeEvents.OnShowGauge -= Show;
            GrabEscapeEvents.OnUpdateGauge -= UpdateValue;
            GrabEscapeEvents.OnHideGauge -= Hide;
        }

        private void Update()
        {
            if (!_isVisible)
                return;

            // 電撃演出：ゲージ全体を点滅させる
            float alpha = Mathf.Lerp(
                _blinkAlphaMin,
                1f,
                (Mathf.Sin(Time.time * _blinkFrequency * Mathf.PI * 2f) + 1f) * 0.5f
            );
            _canvasGroup.alpha = alpha;
        }

        public void Show(float current, float max)
        {
            _currentValue = current;
            _maxValue = max;
            SetVisible(true);
            RefreshGauge();
        }

        public void Hide()
        {
            SetVisible(false);
        }

        public void UpdateValue(float current, float max)
        {
            _currentValue = current;
            _maxValue = max;
            if (_isVisible)
            {
                RefreshGauge();
            }
        }

        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.blocksRaycasts = visible;
                _canvasGroup.interactable = visible;
            }
        }

        private void RefreshGauge()
        {
            float ratio = _maxValue > 0f ? Mathf.Clamp01(_currentValue / _maxValue) : 0f;

            if (_gaugeImage != null)
            {
                _gaugeImage.fillAmount = ratio;
                _gaugeImage.color = Color.Lerp(_colorLow, _colorHigh, ratio);
            }

            if (_labelText != null)
            {
                _labelText.text = $"{Mathf.RoundToInt(ratio * 100f)}%";
            }
        }
    }
}
