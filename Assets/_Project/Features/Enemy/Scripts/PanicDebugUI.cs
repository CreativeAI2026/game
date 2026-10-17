using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// PanicDetector の焦り度スコアと各指標をリアルタイムでデバッグ表示するUIコンポーネント。
    /// 専用の独立キャンバスにアタッチして使用する。
    /// このUIは敵の挙動制御には一切関わらない純粋なデバッグ用途。
    /// エディター上でのみ表示でき、切替キー（既定: F2）で表示/非表示を切り替える。
    /// </summary>
    [RequireComponent(typeof(Canvas))]
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

        [Header("表示切替")]
        [Tooltip("表示/非表示を切り替えるキー。エディター上で再生中のみ有効（ビルドでは常に非表示）")]
        [SerializeField]
        private Key _toggleKey = Key.F2;

        [Tooltip("再生開始時に表示しておくか")]
        [SerializeField]
        private bool _visibleOnStart = false;

        private float _flashTimer = 0f;
        private bool _isFlashing = false;

        private Canvas _canvas;

        private void Awake()
        {
#if UNITY_EDITOR
            // GameObject ごと無効にすると Update が止まり切替キーを受け付けられないため、Canvas の描画だけを切り替える
            _canvas = GetComponent<Canvas>();
            _canvas.enabled = _visibleOnStart;

            if (_panicDetector == null)
                _panicDetector = FindAnyObjectByType<PanicDetector>(FindObjectsInactive.Exclude);
#else
            // 開発中の確認用の表示なので、ビルドには出さない
            gameObject.SetActive(false);
#endif
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
            HandleToggleInput();

            if (_panicDetector == null || !_canvas.enabled)
                return;

            UpdatePanicBar();
            UpdateSignalRows();
            UpdateFlash();
        }

        private void HandleToggleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[_toggleKey].wasPressedThisFrame)
                _canvas.enabled = !_canvas.enabled;
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
            FlightSignal flight = _panicDetector.Flight;
            string flightStatus = _panicDetector.IsFlightDetectionActive
                ? $"逃避    継続: {flight.Timer:F1}s"
                : "逃避    (交戦中の敵なし)";
            UpdateRow(0, flight.Score, flightStatus);

            // 1: スパム
            GuardSpamSignal guardSpam = _panicDetector.GuardSpam;
            UpdateRow(1, guardSpam.Score, $" {guardSpam.Rate:F1}回/s");

            // 2: ジッター
            AimJitterSignal aimJitter = _panicDetector.AimJitter;
            bool isAiming = aimJitter.Score > 0f || aimJitter.AngularSpeed > 0f;
            string jitterStatus = isAiming
                ? $"{aimJitter.AngularSpeed:F0}°/s"
                : "(非構え中/非交戦)";
            UpdateRow(2, aimJitter.Score, jitterStatus);

            // 3: 空間喪失
            TrapSignal trap = _panicDetector.Trap;
            UpdateRow(3, trap.Score, $"継続: {trap.Timer:F1}s");

            // 4: 意思崩壊
            AimCancelSignal aimCancel = _panicDetector.AimCancel;
            UpdateRow(4, aimCancel.Score, $"直近{aimCancel.Window:F0}s: {aimCancel.Count}回");

            // 5: 回避失敗
            HitChainSignal hitChain = _panicDetector.HitChain;
            UpdateRow(5, hitChain.Score, $"連続: {hitChain.ChainCount}回");
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
