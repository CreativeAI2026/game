using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// PlayerStatusのHP変動イベントを購読し、UIスライダーとテキストに反映する。
    /// </summary>
    public class PlayerHpView : MonoBehaviour
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

        [Header("HPバーグラデーション")]
        [Tooltip("HPバーにアタッチされたUIGradientコンポーネント")]
        [SerializeField]
        private UIGradient _hpBarGradient;

        [Header("EKG Imageの設定")]
        [Tooltip("EKGテクスチャ・スクロール速度を適用するImageコンポーネント")]
        [SerializeField]
        private Image _ekgImage;

        [Tooltip("UI_HpEkg マテリアル本体（元のアセット）")]
        [SerializeField]
        private Material _ekgMaterial;

        // ランタイム用のマテリアルインスタンス（アセット本体を汚染しないため）
        private Material _ekgMaterialInstance;

        [System.Serializable]
        private sealed class EkgPhase
        {
            [Tooltip("HP割合の上限（0〜1）。\n次に設定した値以上、この値以下の間で適用される")]
            public float hpRatioThreshold = 1f;

            [Tooltip("このHP割合帯で表示するテクスチャ")]
            public Texture2D texture = null;

            [Tooltip("このHP割合帯での心電図スクロール速度")]
            public float scrollSpeed = 0.25f;

            [Tooltip("HPバーの左端の色")]
            public Color hpBarLeftColor = Color.white;

            [Tooltip("HPバーの右端の色")]
            public Color hpBarRightColor = Color.white;
        }

        [Header("EKGフェーズ設定")]
        [Tooltip(
            "HP割合に応じたEKGのフェーズ設定。hpRatioThresholdの大きい順（1.0 → 0.5 → 0.2）に並べる。"
        )]
        [SerializeField]
        private EkgPhase[] _ekgPhases;

        /// <summary>心電図(EKG)を描く範囲。</summary>
        private enum EkgDisplayArea
        {
            [InspectorName("バー全体(背景の上)")]
            WholeBar,

            [InspectorName("残りHP(Fill)の中だけ")]
            FillOnly,
        }

        [Header("見た目の切り替え(プレイ中に変えてもすぐ反映)")]
        [Tooltip(
            "心電図を描く範囲。「残りHP(Fill)の中だけ」は、EKG の Image を Filled にして Fill の右端より右を描かない"
        )]
        [SerializeField]
        private EkgDisplayArea _ekgArea = EkgDisplayArea.WholeBar;

        [Tooltip("オンで HP バーのグラデーションの左右(始端と末端)の色を入れ替える")]
        [SerializeField]
        private bool _reverseHpBarGradient;

        // プレイヤーは Prefab から実行時に生成されるので、PlayerRegistry から受け取る(未生成の間は null)
        private PlayerStatus _playerStatus;

        // 実際の HP(プレビュー解除時・設定変更時に描き直すため保持する)
        private float _actualHp;
        private float _actualMaxHp;
        private bool _hasActualHp;

        // 確認用に表示だけ差し替える HP 割合(null で実際の HP を表示)
        private float? _previewHpRatio;

        private Image.Type _ekgOriginalImageType;
        private readonly Vector3[] _fillCorners = new Vector3[4];

        /// <summary>確認用に HP 割合を差し替えて表示中か。</summary>
        public bool IsPreviewing => _previewHpRatio.HasValue;

        /// <summary>
        /// 確認用: 実際の HP を変えずに、表示だけ指定の HP 割合(0〜1)にする。null で実際の HP の表示に戻す。
        /// ダメージを受けなくても、HP 帯ごとの色・心電図を確かめられるようにするためのもの。
        /// </summary>
        public void SetPreviewHpRatio(float? ratio)
        {
            _previewHpRatio = ratio.HasValue ? Mathf.Clamp01(ratio.Value) : null;
            Redraw();
        }

        /// <summary>HP 帯の境目(hpRatioThreshold)の一覧。確認用 Inspector のボタンに使う。</summary>
        public float[] GetPhaseThresholds()
        {
            var thresholds = new float[_ekgPhases.Length];
            for (int i = 0; i < _ekgPhases.Length; i++)
                thresholds[i] = _ekgPhases[i].hpRatioThreshold;
            return thresholds;
        }

        private void Awake()
        {
            if (_ekgImage != null)
                _ekgOriginalImageType = _ekgImage.type;

            // マテリアルをインスタンス化してImageに割り当てる。
            // SharedマテリアルをそのままSetTexture/SetFloatするとアセットが汚染され
            // Playモード開始時にテクスチャが黒くなる問題を防ぐ。
            if (_ekgImage != null && _ekgMaterial != null)
            {
                _ekgMaterialInstance = new Material(_ekgMaterial);
                _ekgImage.material = _ekgMaterialInstance;
            }
        }

        private void OnDestroy()
        {
            // 動的生成したインスタンスはDestroy時に明示的に破棄する
            if (_ekgMaterialInstance != null)
            {
                Destroy(_ekgMaterialInstance);
            }
        }

        void OnEnable()
        {
            PlayerRegistry.Changed += Bind;
            Bind(PlayerRegistry.Current);
        }

        private void OnDisable()
        {
            PlayerRegistry.Changed -= Bind;
            Bind(null);
        }

        private void Start()
        {
            // PlayerStatus.Start()がPlayerHpView.OnEnable()より先に実行された場合、
            // OnHpChangedの発火を受け取れないため、Start()で明示的に初期UIを更新する。
            if (_playerStatus != null)
            {
                UpdateUI(_playerStatus.CurrentHp, _playerStatus.CurrentMaxHp);
            }
        }

        /// <summary>購読先のプレイヤーを切り替える(未生成・破棄時は null)。</summary>
        private void Bind(PlayerController player)
        {
            if (_playerStatus != null)
                _playerStatus.OnHpChanged -= UpdateUI;

            _playerStatus = player != null ? player.GetComponent<PlayerStatus>() : null;

            if (_playerStatus != null)
            {
                _playerStatus.OnHpChanged += UpdateUI;
                // 後から生成されたプレイヤーでも、購読した時点の値で表示を合わせる
                UpdateUI(_playerStatus.CurrentHp, _playerStatus.CurrentMaxHp);
            }
        }

        // プレイ中に Inspector で切り替えたら、その場で描き直す
        private void OnValidate()
        {
            if (Application.isPlaying && isActiveAndEnabled)
                Redraw();
        }

        private void UpdateUI(float currentHp, float maxHp)
        {
            _actualHp = currentHp;
            _actualMaxHp = maxHp;
            _hasActualHp = true;
            Redraw();
        }

        /// <summary>実際の HP(プレビュー中はプレビューの割合)で表示を作り直す。</summary>
        private void Redraw()
        {
            // プレイヤー未生成でもプレビューできるよう、最大 HP はスライダーの値で代用する
            float maxHp = _hasActualHp ? _actualMaxHp : _hpSlider.maxValue;
            if (!_previewHpRatio.HasValue && !_hasActualHp)
                return;

            float currentHp = _previewHpRatio.HasValue ? maxHp * _previewHpRatio.Value : _actualHp;
            Draw(currentHp, maxHp);
        }

        private void Draw(float currentHp, float maxHp)
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

            float ratio = maxHp > 0f ? currentHp / maxHp : 0f;
            EkgPhase phase = SelectPhase(ratio);
            ApplyEkgPhase(phase);
            ApplyHpBarColor(phase);
            ApplyEkgArea();
        }

        /// <summary>
        /// 心電図を描く範囲を反映する。EKG のシェーダーは UI 用ではなく RectMask2D / Mask で切り抜けないため、
        /// Image を Filled(横・左から)にして、Fill の右端までだけ描く。
        /// </summary>
        private void ApplyEkgArea()
        {
            if (_ekgImage == null)
                return;

            if (_ekgArea == EkgDisplayArea.WholeBar)
            {
                _ekgImage.type = _ekgOriginalImageType;
                return;
            }

            _ekgImage.type = Image.Type.Filled;
            _ekgImage.fillMethod = Image.FillMethod.Horizontal;
            _ekgImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            _ekgImage.fillAmount = FillRightEdgeOnEkg();
        }

        /// <summary>Fill の右端が、EKG の横幅のどこ(0=左端〜1=右端)にあるか。</summary>
        private float FillRightEdgeOnEkg()
        {
            RectTransform ekgRect = _ekgImage.rectTransform;
            _hpSlider.fillRect.GetWorldCorners(_fillCorners);
            float fillRight = ekgRect.InverseTransformPoint(_fillCorners[2]).x; // [2] = 右上
            Rect rect = ekgRect.rect;
            return Mathf.Clamp01((fillRight - rect.xMin) / rect.width);
        }

        /// <summary>
        /// HP割合に対応するフェーズを選ぶ。リストは hpRatioThreshold の降順が前提で、
        /// 「threshold >= hpRatio を満たす最後の要素」を選ぶ(例: [1.0, 0.5, 0.2] で ratio=0.4 → Element 1)。
        /// </summary>
        private EkgPhase SelectPhase(float hpRatio)
        {
            EkgPhase selected = _ekgPhases[0];
            for (int i = 0; i < _ekgPhases.Length; i++)
            {
                if (_ekgPhases[i].hpRatioThreshold >= hpRatio)
                {
                    selected = _ekgPhases[i];
                }
            }
            return selected;
        }

        /// <summary>
        /// UI_HpEkg マテリアルのテクスチャとスクロール速度を適用する。
        /// </summary>
        private void ApplyEkgPhase(EkgPhase phase)
        {
            // Awake実行時にフィールドが未設定だった場合の遅延初期化
            if (_ekgMaterialInstance == null && _ekgImage != null && _ekgMaterial != null)
            {
                _ekgMaterialInstance = new Material(_ekgMaterial);
                _ekgImage.material = _ekgMaterialInstance;
            }

            if (_ekgMaterialInstance == null)
                return;

            if (phase.texture != null)
            {
                _ekgMaterialInstance.SetTexture("_MainTexture", phase.texture);
            }
            _ekgMaterialInstance.SetFloat("_ScrollSpeed", phase.scrollSpeed);

            // UI (Canvas) 側にマテリアルの変更を通知して再描画させる
            if (_ekgImage != null)
            {
                _ekgImage.SetMaterialDirty();
                // 念のため再代入でも更新をトリガーする
                _ekgImage.material = _ekgMaterialInstance;
            }
        }

        /// <summary>
        /// UIGradientのleftColor・rightColorを適用する(_reverseHpBarGradient なら左右を入れ替える)。
        /// </summary>
        private void ApplyHpBarColor(EkgPhase phase)
        {
            if (_hpBarGradient == null)
                return;

            _hpBarGradient.leftColor = _reverseHpBarGradient
                ? phase.hpBarRightColor
                : phase.hpBarLeftColor;
            _hpBarGradient.rightColor = _reverseHpBarGradient
                ? phase.hpBarLeftColor
                : phase.hpBarRightColor;
            _hpBarGradient.SetVerticesDirty();
        }
    }
}
