using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 剣の防御メーターの残量を、並べた Point の数で段階的に表示する。
    /// 残量を Point の数で等分し、減るときは右の Point から消す。満タンの間は全体を隠す。
    /// </summary>
    public sealed class GuardGaugeView : MonoBehaviour
    {
        // 割合 × 個数 の浮動小数の誤差(0.9 × 10 = 9.0000004 など)で 1 個多く点かないようにするための許容幅
        private const float SegmentEpsilon = 1e-4f;

        [Tooltip("残量を表す Point を左から順に並べる。個数でメーターを等分する(10 個なら 10% ずつ)")]
        [SerializeField]
        private Image[] _points;

        [Tooltip("ゲージ全体の表示・非表示を切り替える CanvasGroup")]
        [SerializeField]
        private CanvasGroup _canvasGroup;

        [Tooltip("防御できる間の Point の色(画像の色に掛け合わせる。白で画像のまま)")]
        [SerializeField]
        private Color _normalColor = Color.white;

        [Tooltip("防御が割れて、構えられない間の Point の色(画像の色に掛け合わせる)")]
        [SerializeField]
        private Color _brokenColor = new(1f, 0.3f, 0.25f);

        // プレイヤーは Prefab から実行時に生成されるので、PlayerRegistry から受け取る(未生成の間は null)
        private SwordController _sword;

        // 前回反映した表示。変わった時だけ Image を書き換える(-1 = まだ反映していない)
        private int _shownCount = -1;
        private bool _shownBroken;

        /// <summary>
        /// 残量の割合(0〜1)に対して点ける Point の数。切り上げなので、残りが少しでもあれば 1 個は点き、
        /// 0 個になるのは空(防御が割れた時)だけ。
        /// </summary>
        public static int CountLitPoints(float ratio, int pointCount)
        {
            int lit = Mathf.CeilToInt(Mathf.Clamp01(ratio) * pointCount - SegmentEpsilon);
            return Mathf.Clamp(lit, 0, pointCount);
        }

        private void Awake()
        {
            Assert.IsTrue(_points != null && _points.Length > 0, $"{name}: Point が未設定です");
            for (int i = 0; i < _points.Length; i++)
                Assert.IsNotNull(_points[i], $"{name}: Point の {i} 番目が未設定です");
            Assert.IsNotNull(_canvasGroup, $"{name}: CanvasGroup が未設定です");
        }

        private void OnEnable()
        {
            PlayerRegistry.Changed += Bind;
            Bind(PlayerRegistry.Current);
        }

        private void OnDisable()
        {
            PlayerRegistry.Changed -= Bind;
            Bind(null);
        }

        /// <summary>表示するプレイヤーを切り替える(未生成・破棄時は null)。</summary>
        private void Bind(PlayerController player)
        {
            // 剣は持ち替え中は非表示なので、非表示のものも含めて探す
            _sword = player != null ? player.GetComponentInChildren<SwordController>(true) : null;
        }

        private void Update()
        {
            if (_sword == null)
            {
                _canvasGroup.alpha = 0f;
                return;
            }

            GuardGauge gauge = _sword.GuardGauge;
            float now = Time.time;
            float ratio = gauge.Max > 0f ? gauge.GetCurrent(now) / gauge.Max : 0f;

            _canvasGroup.alpha = ratio < 1f ? 1f : 0f;
            ShowPoints(CountLitPoints(ratio, _points.Length), gauge.IsBroken(now));
        }

        /// <summary>左から count 個の Point を点け、残りを消す。</summary>
        private void ShowPoints(int count, bool isBroken)
        {
            if (count == _shownCount && isBroken == _shownBroken)
                return;

            Color color = isBroken ? _brokenColor : _normalColor;
            for (int i = 0; i < _points.Length; i++)
            {
                _points[i].enabled = i < count;
                _points[i].color = color;
            }

            _shownCount = count;
            _shownBroken = isBroken;
        }
    }
}
