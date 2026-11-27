using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 装備のバフで最大HPが増えている間、増えた分の HP を HP バーの上のゲージに出す。
    /// 残量が 0 になってもバフが付いている間は枠ごと出したままにし、装備を変えてバフが外れたら隠す。
    /// </summary>
    public sealed class MaxHpBonusGaugeView : MonoBehaviour
    {
        [Tooltip("バフが付いている間だけ表示する、ゲージ全体(枠と塗り)の親")]
        [SerializeField]
        private GameObject _gaugeRoot;

        [Tooltip("残量を表す塗りの Image(Image Type を Filled・横・左からにしておく)")]
        [SerializeField]
        private Image _fillImage;

        // プレイヤーは Prefab から実行時に生成されるので、PlayerRegistry から受け取る(未生成の間は null)
        private PlayerStatus _playerStatus;

        private void Awake()
        {
            Assert.IsNotNull(_gaugeRoot, $"{name}: ゲージの親(_gaugeRoot)が未設定です");
            Assert.IsNotNull(_fillImage, $"{name}: 塗りの Image(_fillImage)が未設定です");
            Assert.AreEqual(
                Image.Type.Filled,
                _fillImage.type,
                $"{name}: 塗りの Image の Image Type を Filled にしてください"
            );
            // このコンポーネント自身は購読を続けるため、表示の切り替えは子の _gaugeRoot で行う
            Assert.AreNotEqual(
                gameObject,
                _gaugeRoot,
                $"{name}: ゲージの親には、このオブジェクトの子を指定してください"
            );
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

        /// <summary>購読先のプレイヤーを切り替える(未生成・破棄時は null)。</summary>
        private void Bind(PlayerController player)
        {
            if (_playerStatus != null)
                _playerStatus.HpChanged -= OnHpChanged;

            _playerStatus = player != null ? player.GetComponent<PlayerStatus>() : null;

            if (_playerStatus != null)
            {
                _playerStatus.HpChanged += OnHpChanged;
                // 後から生成されたプレイヤーでも、購読した時点の値で表示を合わせる
                OnHpChanged(_playerStatus.CurrentHp, _playerStatus.CurrentMaxHp);
            }
            else
            {
                _gaugeRoot.SetActive(false);
            }
        }

        // 装備や武器の変更でも HpChanged が来るので、バフの付け外しもここで拾える
        private void OnHpChanged(float currentHp, float maxHp)
        {
            HpGaugeSplit split = HpGaugeSplit.Calculate(
                currentHp,
                maxHp,
                _playerStatus.EquipmentMaxHpBonus
            );
            _gaugeRoot.SetActive(split.HasBonus);
            _fillImage.fillAmount = split.BonusRatio;
        }
    }
}
