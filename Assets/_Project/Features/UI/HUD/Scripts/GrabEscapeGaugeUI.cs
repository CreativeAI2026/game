using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public class GrabEscapeGaugeUI : MonoBehaviour
    {
        public static GrabEscapeGaugeUI Instance { get; private set; }

        [Header("アイコン")]
        [SerializeField]
        private Image _iconImage;

        [Tooltip("通常時の画像")]
        [SerializeField]
        private Sprite _normalSprite;

        [Tooltip("振りほどき度が増えた瞬間に表示する画像")]
        [SerializeField]
        private Sprite _pressedSprite;

        [Tooltip("押下画像を表示してから通常画像に戻るまでの秒数")]
        [SerializeField]
        private float _pressedDuration = 0.1f;

        [Header("ラベル")]
        [SerializeField]
        private TextMeshProUGUI _labelText;

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
        private float _pressedTimer = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_iconImage == null || _normalSprite == null || _pressedSprite == null)
            {
                Debug.LogError(
                    $"[{nameof(GrabEscapeGaugeUI)}] _iconImage / _normalSprite / _pressedSprite のいずれかが未設定です",
                    this
                );
            }

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

            UpdatePressedIcon();
        }

        public void Show(float current, float max)
        {
            _currentValue = current;
            _maxValue = max;
            SetVisible(true);
            SetIconSprite(_normalSprite);
            _pressedTimer = 0f;
            RefreshLabel();
        }

        public void Hide()
        {
            SetVisible(false);
        }

        public void UpdateValue(float current, float max)
        {
            bool increased = current > _currentValue;
            _currentValue = current;
            _maxValue = max;
            if (!_isVisible)
                return;

            if (increased)
            {
                SetIconSprite(_pressedSprite);
                _pressedTimer = _pressedDuration;
            }
            RefreshLabel();
        }

        private void UpdatePressedIcon()
        {
            if (_pressedTimer <= 0f)
                return;

            _pressedTimer -= Time.deltaTime;
            if (_pressedTimer <= 0f)
            {
                SetIconSprite(_normalSprite);
            }
        }

        private void SetIconSprite(Sprite sprite)
        {
            if (_iconImage == null)
                return;

            _iconImage.sprite = sprite;
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

        private void RefreshLabel()
        {
            float ratio = _maxValue > 0f ? Mathf.Clamp01(_currentValue / _maxValue) : 0f;

            if (_labelText != null)
            {
                _labelText.text = $"{Mathf.RoundToInt(ratio * 100f)}%";
            }
        }
    }
}
