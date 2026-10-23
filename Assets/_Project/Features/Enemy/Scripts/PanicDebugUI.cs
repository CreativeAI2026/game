using System.Collections.Generic;
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
    /// エディター・ビルドの両方で動作し、切替キー（既定: F2）で表示/非表示を切り替える。
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class PanicDebugUI : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField]
        private PanicDetector _panicDetector;

        [Tooltip(
            "焦りに応じて追い込むボス。未設定ならシーンから探す（いなければ追い込みの行は出さない）"
        )]
        [SerializeField]
        private TutorialBossController _boss;

        [Header("UIパーツ")]
        [Tooltip("ボスの追い込みの状態と、次の攻撃までの時間を出すテキスト（2行）")]
        [SerializeField]
        private TextMeshProUGUI _bossPressureText;

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
            "各指標の名前テキスト。並びは PanicDetector.Signals の順（逃避・スパム・ジッター・空間喪失・意思崩壊・回避失敗・硬直・武器切替）。\n"
                + "未設定の行はシーン側の文字のまま"
        )]
        [SerializeField]
        private TextMeshProUGUI[] _signalLabels = new TextMeshProUGUI[0];

        [Tooltip("各指標の計測値テキスト。並びは名前テキストと同じ。行が足りない指標は表示しない")]
        [SerializeField]
        private TextMeshProUGUI[] _signalTexts = new TextMeshProUGUI[0];

        [Tooltip(
            "各指標のバー（Filledタイプ）。並びは名前テキストと同じ。行が足りない指標は表示しない"
        )]
        [SerializeField]
        private Image[] _signalBars = new Image[0];

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
        [Tooltip("表示/非表示を切り替えるキー。エディター・ビルドの両方で有効")]
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
            // GameObject ごと無効にすると Update が止まり切替キーを受け付けられないため、Canvas の描画だけを切り替える
            _canvas = GetComponent<Canvas>();
            _canvas.enabled = _visibleOnStart;

            if (_panicDetector == null)
                _panicDetector = FindAnyObjectByType<PanicDetector>(FindObjectsInactive.Exclude);

            if (_boss == null)
                _boss = FindAnyObjectByType<TutorialBossController>(FindObjectsInactive.Exclude);
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
            UpdateBossPressure();
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
                PanicLevel level = _panicDetector.Level;
                _panicScoreText.text =
                    level == PanicLevel.Calm
                        ? $"{Mathf.RoundToInt(score)} {level.ToDisplayName()}"
                        : $"{Mathf.RoundToInt(score)} {level.ToDisplayName()}/{_panicDetector.DominantCategory.ToDisplayName()}";
            }
        }

        private void UpdateSignalRows()
        {
            // 指標を足しても表示コードを直さずに済むよう、指標の一覧と生の値（単位付き）から行を作る
            bool isInCombat = _panicDetector.IsInCombat;
            IReadOnlyList<PanicSignal> signals = _panicDetector.Signals;
            for (int i = 0; i < signals.Count; i++)
            {
                PanicSignal signal = signals[i];
                if (i < _signalLabels.Length && _signalLabels[i] != null)
                {
                    _signalLabels[i].text = signal.DisplayName;
                }

                // 回避失敗以外は交戦中しか計測しないので、交戦外は値ではなく理由を出す
                string status = isInCombat
                    ? $"{signal.RawValueUnit}: {signal.RawValue:0.#}"
                    : "(非交戦)";
                UpdateRow(i, signal.Score, status);
            }
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

        /// <summary>
        /// ボスの追い込みの状態を出す。例:
        ///   ボスの追い込み：追い込み中 逃げ腰（残り5.2秒）
        ///   次の攻撃まで：1.2秒
        /// </summary>
        private void UpdateBossPressure()
        {
            if (_bossPressureText == null)
                return;

            if (_boss == null)
            {
                _bossPressureText.text = string.Empty;
                return;
            }

            TBossPanicPressure pressure = _boss.PanicPressure;
            string status = pressure.Mode switch
            {
                TBossPressureMode.Pressing =>
                    $"{pressure.Mode.ToDisplayName()} {pressure.Category.ToDisplayName()}（残り{pressure.PressureRemaining:F1}秒）",
                TBossPressureMode.Breather =>
                    $"{pressure.Mode.ToDisplayName()}（残り{pressure.BreatherRemaining:F1}秒）",
                _ => pressure.Mode.ToDisplayName(),
            };

            float untilAttack = _boss.TimeUntilNextAttack;
            string nextAttack = untilAttack < 0f ? "（非交戦）" : $"{untilAttack:F1}秒";

            _bossPressureText.text = $"ボスの追い込み：{status}\n次の攻撃まで：{nextAttack}";
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
                // 見出し「最後の検知：」はシーン側の固定ラベルにあるので、ここでは指標名だけを出す
                _lastSignalText.text = $"[{signalName}]";
            }

            _flashTimer = _flashDuration;
            _isFlashing = true;
        }
    }
}
