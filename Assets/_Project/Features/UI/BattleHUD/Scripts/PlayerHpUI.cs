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

        [Header("HPバーグラデーション")]
        [Tooltip("HPバーにアタッチされたUIGradientコンポーネント")]
        [SerializeField]
        private UIGradient _hpBarGradient;

        [Header("EKG Imageの設定")]
        [Tooltip("EKGテクスチャ・スクロール速度を適用するImageコンポーネント")]
        [SerializeField]
        private Image _ekgImage;

        [Tooltip("EKG_Material本体（元のアセット）")]
        [SerializeField]
        private Material _ekgMaterial;

        // ランタイム用のマテリアルインスタンス（アセット本体を汚染しないため）
        private Material _ekgMaterialInstance;

        [System.Serializable]
        private struct EkgPhase
        {
            [Tooltip("HP割合の上限（0〜1）。\n次に設定した値以上、この値以下の間で適用される")]
            public float hpRatioThreshold;

            [Tooltip("このHP割合帯で表示するテクスチャ")]
            public Texture2D texture;

            [Tooltip("このHP割合帯での心電図スクロール速度")]
            public float scrollSpeed;

            [Tooltip("HPバーの左端の色")]
            public Color hpBarLeftColor;

            [Tooltip("HPバーの右端の色")]
            public Color hpBarRightColor;
        }

        [Header("EKGフェーズ設定")]
        [Tooltip(
            "HP割合に応じたEKGのフェーズ設定。hpRatioThresholdの大きい順（1.0 → 0.5 → 0.2）に並べる。"
        )]
        [SerializeField]
        private EkgPhase[] _ekgPhases;

        [Header("参照")]
        [Tooltip("プレイヤーのステータス")]
        [SerializeField]
        private PlayerStatus _playerStatus;

        private void Awake()
        {
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

        private void Start()
        {
            // PlayerStatus.Start()がPlayerHpUI.OnEnable()より先に実行された場合、
            // OnHpChangedの発火を受け取れないため、Start()で明示的に初期UIを更新する。
            if (_playerStatus != null)
            {
                UpdateUI(_playerStatus.CurrentHp, _playerStatus.CurrentMaxHp);
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

            float ratio = maxHp > 0f ? currentHp / maxHp : 0f;
            EkgPhase phase = SelectPhase(ratio);
            ApplyEkgPhase(phase);
            ApplyHpBarColor(phase);
        }

        /// <summary>
        /// HP割合に対応するフェーズを選択する。
        /// リストは hpRatioThreshold の降順（大→小）を前提とする。
        /// 「threshold >= hpRatio を満たす最後の要素」を選ぶことで、
        /// 1.0 / 0.5 / 0.2 の順で正しく振り分ける。
        ///
        /// 例）thresholds = [1.0, 0.5, 0.2]
        ///   ratio=0.8 → 1.0>=0.8 のみ真 → Element 0
        ///   ratio=0.4 → 1.0>=0.4, 0.5>=0.4 が真 → 最後の Element 1
        ///   ratio=0.1 → 1.0>=0.1, 0.5>=0.1, 0.2>=0.1 が真 → 最後の Element 2
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
        /// EKG_Materialのテクスチャとスクロール速度を適用する。
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
        /// UIGradientのleftColor・rightColorを適用する。
        /// </summary>
        private void ApplyHpBarColor(EkgPhase phase)
        {
            if (_hpBarGradient == null)
                return;

            _hpBarGradient.leftColor = phase.hpBarLeftColor;
            _hpBarGradient.rightColor = phase.hpBarRightColor;
            _hpBarGradient.SetVerticesDirty();
        }
    }
}
