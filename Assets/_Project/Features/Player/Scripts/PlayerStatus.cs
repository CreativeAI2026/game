using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーのステータス（HP、攻撃力、防御力等）を管理する。
    /// ベースパラメータはPlayerParameterData（ScriptableObject）で定義し、
    /// 装備やバフによる加算は各プロパティ内で計算する拡張ポイントを持つ。
    /// </summary>
    public class PlayerStatus : MonoBehaviour, IDamageable
    {
        [SerializeField]
        private PlayerParameterData _playerData;

        public float CurrentHp { get; private set; }

        // 装備による補正合計(素の値に足すと最終ステータス)。装備変更で再計算する。
        private EquipmentBonus _equipment;

        // HPが変動したときに通知するデリゲート。引数は（現在のHP、最大HP）
        public Action<float, float> OnHpChanged;

        // 装備変更などで攻撃/防御/最大HP等が変わったときの通知(HUD等が最終値を読み直す)。
        public event Action OnStatsChanged;

        private PlayerFlinchHandler _flinchHandler;

        private void Awake()
        {
            _flinchHandler = GetComponent<PlayerFlinchHandler>();
        }

        private void OnEnable()
        {
            // 静的イベント:InventoryManager が後から生成されても購読は成立する。
            InventoryManager.EquipmentChanged += RecalculateFromInventory;
        }

        private void OnDisable()
        {
            InventoryManager.EquipmentChanged -= RecalculateFromInventory;
        }

        public float CurrentMaxHp
        {
            get
            {
                float buffBonus = 0f;
                return _playerData.baseMaxLife + _equipment.maxHp + buffBonus;
            }
        }

        public float CurrentAttackPower
        {
            get
            {
                float buffBonus = 0f;
                return _playerData.baseAttackPower + _equipment.attack + buffBonus;
            }
        }

        public float CurrentDefense
        {
            get
            {
                float buffBonus = 0f;
                return _playerData.baseDefense + _equipment.defense + buffBonus;
            }
        }

        public float CurrentCriticalChance
        {
            get
            {
                float buffBonus = 0f;

                float finalCriticalChance =
                    _playerData.baseCriticalChance + _equipment.criticalChance + buffBonus;

                if (finalCriticalChance > 100f)
                {
                    return 100f;
                }
                else if (finalCriticalChance < 0f)
                {
                    return 0f;
                }

                return finalCriticalChance;
            }
        }

        public float CurrentCriticalDamageRatio
        {
            get
            {
                float buffBonus = 0f;
                return _playerData.baseCriticalDamageRatio + _equipment.criticalDamage + buffBonus;
            }
        }

        private void Start()
        {
            _equipment =
                InventoryManager.Instance != null
                    ? InventoryManager.Instance.GetEquippedBonus()
                    : default;
            CurrentHp = CurrentMaxHp; // 装備込みの最大HPで満タン開始
            OnHpChanged?.Invoke(CurrentHp, CurrentMaxHp);
            OnStatsChanged?.Invoke();
        }

        /// <summary>装備中アイテムから補正を引き直して最終ステータスへ反映する(装備変更時に呼ばれる)。</summary>
        public void RecalculateFromInventory()
        {
            SetEquipment(
                InventoryManager.Instance != null
                    ? InventoryManager.Instance.GetEquippedBonus()
                    : default
            );
        }

        /// <summary>装備補正を差し替えて最終ステータスを更新する。最大HP減少時は現在HPをクランプする。</summary>
        public void SetEquipment(EquipmentBonus bonus)
        {
            _equipment = bonus;
            OnStatusChanged(); // 現在HP > 最大HP のクランプ
            OnStatsChanged?.Invoke();
            OnHpChanged?.Invoke(CurrentHp, CurrentMaxHp);
        }

        public void TakeDamage(float damage, bool isCritical)
        {
            // 防御力で軽減するが、最低1ダメージは保証する（防御力がダメージを上回ってもノーダメージにはしない）
            float finalDamage = Mathf.Max(1f, damage - CurrentDefense);

            CurrentHp -= finalDamage;
            OnHpChanged?.Invoke(CurrentHp, CurrentMaxHp);
            Debug.Log(
                $"プレイヤーは {finalDamage} ダメージを受けた！ 残りHP: {CurrentHp}/{CurrentMaxHp}"
            );

            CameraShakeManager.Instance?.Shake(0.5f);
            DamageVignette.Instance?.TriggerVignette();
            _flinchHandler?.TriggerFlinch();

            if (CurrentHp <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// 武器固有の倍率を受け取り、会心判定を含めた最終ダメージを算出する。
        /// ダメージ計算を一元化することで、装備やバフの影響を武器側に意識させない設計。
        /// </summary>
        /// <param name="weaponMultiplier">武器固有の倍率（弓なら0.8f、近接なら1.5fなど）</param>
        /// <param name="isCritical">会心が発生したかどうかの結果を返す</param>
        public float RollDamage(float weaponMultiplier, out bool isCritical)
        {
            float baseDmg = CurrentAttackPower * weaponMultiplier;

            isCritical = UnityEngine.Random.Range(0f, 100f) <= CurrentCriticalChance;
            if (isCritical)
            {
                baseDmg += baseDmg * CurrentCriticalDamageRatio;
            }

            return baseDmg;
        }

        public void Heal(float amount)
        {
            CurrentHp = Mathf.Min(CurrentHp + amount, CurrentMaxHp);
            OnHpChanged?.Invoke(CurrentHp, CurrentMaxHp);
            Debug.Log($"プレイヤーは {amount} 回復した！ 残りHP: {CurrentHp}/{CurrentMaxHp}");
        }

        /// <summary>
        /// 装備の着脱やバフ終了等で最大HPが変動したときに呼ぶ。
        /// 最大HPが下がった場合に、現在HPが最大HPを超えないよう補正する。
        /// </summary>
        public void OnStatusChanged()
        {
            if (CurrentHp > CurrentMaxHp)
            {
                CurrentHp = CurrentMaxHp;
            }
        }

        private void Die()
        {
            Debug.Log("プレイヤーは力尽きた...");
            // ゲームオーバー処理やアニメーション再生など
        }
    }
}
