using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// PanicDetector の焦り度スコアと各指標をリアルタイムでデバッグ表示するUIコンポーネント。
    /// 専用の独立キャンバスにアタッチして使用する。
    /// このUIは敵の挙動制御には一切関わらない純粋なデバッグ用途。
    /// </summary>
    public class PanicDebugUI : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField]
        private PanicDetector _panicDetector;

        [Header("UIパーツ")]
        [Tooltip("焦り度バー（Filledタイプ）")]
        [SerializeField]
        private Image _panicFillBar;

        [Tooltip("焦り度の数値テキスト")]
        [SerializeField]
        private TextMeshProUGUI _panicScoreText;

        [Tooltip("最後に検知したシグナル名テキスト")]
        [SerializeField]
        private TextMeshProUGUI _lastSignalText;

        [Tooltip(
            "各指標ごとの情報テキスト（6行、順番通り）\n0:逃避 1:スパム 2:ジッター 3:空間喪失 4:意思崩壊 5:回避失敗"
        )]
        [SerializeField]
        private TextMeshProUGUI[] _signalTexts = new TextMeshProUGUI[6];

        [Tooltip(
            "各指標のバー（Filledタイプ、6本）\n0:逃避 1:スパム 2:ジッター 3:空間喪失 4:意思崩壊 5:回避失敗"
        )]
        [SerializeField]
        private Image[] _signalBars = new Image[6];

        [Header("カラー設定")]
        [Tooltip("焦り度が低いときのバーの色")]
        [SerializeField]
        private Color _calmColor = new Color(0.2f, 0.8f, 0.4f);

        [Tooltip("焦り度が高いときのバーの色")]
        [SerializeField]
        private Color _panicColor = new Color(0.95f, 0.2f, 0.2f);

        [Header("点滅設定")]
        [Tooltip("シグナル検知時に最後の検知テキストを点滅させる秒数")]
        [SerializeField]
        private float _flashDuration = 0.8f;

        private float _flashTimer = 0f;
        private bool _isFlashing = false;

        private void Awake()
        {
            if (_panicDetector == null)
                _panicDetector = FindAnyObjectByType<PanicDetector>();
        }

        private void OnEnable()
        {
            if (_panicDetector != null)
                _panicDetector.OnPanicSignalDetected += OnSignalDetected;
        }

        private void OnDisable()
        {
            if (_panicDetector != null)
                _panicDetector.OnPanicSignalDetected -= OnSignalDetected;
        }

        private void Update()
        {
            if (_panicDetector == null)
                return;

            UpdatePanicBar();
            UpdateSignalRows();
            UpdateFlash();
        }

        private void UpdatePanicBar()
        {
            float score = _panicDetector.PanicScore;
            float t = score / 100f;

            if (_panicFillBar != null)
            {
                _panicFillBar.fillAmount = t;
                _panicFillBar.color = Color.Lerp(_calmColor, _panicColor, t);
            }

            if (_panicScoreText != null)
            {
                _panicScoreText.text = Mathf.RoundToInt(score).ToString();
            }
        }

        private void UpdateSignalRows()
        {
            // 0: 逃避
            UpdateRow(
                0,
                _panicDetector.FlightScore,
                $"逃避    継続: {_panicDetector.FlightTimer:F1}s"
            );

            // 1: スパム
            UpdateRow(1, _panicDetector.SpamScore, $" {_panicDetector.SpamRate:F1}回/s");

            // 2: ジッター
            bool isAiming =
                _panicDetector.JitterScore > 0f || _panicDetector.JitterAngularSpeed > 0f;
            string jitterStatus = isAiming
                ? $"{_panicDetector.JitterAngularSpeed:F0}°/s"
                : "(非構え中)";
            UpdateRow(2, _panicDetector.JitterScore, jitterStatus);

            // 3: 空間喪失
            UpdateRow(
                3,
                _panicDetector.TrapScore,
                $"継続: {_panicDetector.TrapTimer:F1}s"
            );

            // 4: 意思崩壊
            UpdateRow(
                4,
                _panicDetector.AimCancelScore,
                $"累計: {_panicDetector.AimCancelCount}回"
            );

            // 5: 回避失敗
            UpdateRow(
                5,
                _panicDetector.HitChainScore,
                $"連続: {_panicDetector.HitChainCount}回"
            );
        }

        private void UpdateRow(int index, float score, string label)
        {
            if (index < _signalBars.Length && _signalBars[index] != null)
            {
                _signalBars[index].fillAmount = score;
                _signalBars[index].color = Color.Lerp(_calmColor, _panicColor, score);
            }

            if (index < _signalTexts.Length && _signalTexts[index] != null)
            {
                _signalTexts[index].text = label;
            }
        }

        private void UpdateFlash()
        {
            if (!_isFlashing)
                return;

            _flashTimer -= Time.deltaTime;

            if (_lastSignalText != null)
            {
                // サイン波で透明度を点滅させる
                float alpha = Mathf.Abs(Mathf.Sin(Time.time * 8f));
                Color c = _lastSignalText.color;
                c.a = alpha;
                _lastSignalText.color = c;
            }

            if (_flashTimer <= 0f)
            {
                _isFlashing = false;
                if (_lastSignalText != null)
                {
                    Color c = _lastSignalText.color;
                    c.a = 1f;
                    _lastSignalText.color = c;
                }
            }
        }

        private void OnSignalDetected(string signalName)
        {
            if (_lastSignalText != null)
            {
                _lastSignalText.text = $"最後の検知: [{signalName}]";
            }

            _flashTimer = _flashDuration;
            _isFlashing = true;
        }
    }
}
