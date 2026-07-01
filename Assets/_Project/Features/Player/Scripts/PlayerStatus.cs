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

        // HPが変動したときに通知するデリゲート。引数は（現在のHP、最大HP）
        public Action<float, float> OnHpChanged;
        private PlayerFlinchHandler _flinchHandler;

        private void Awake()
        {
            _flinchHandler = GetComponent<PlayerFlinchHandler>();
        }

        public float CurrentMaxHp
        {
            get
            {
                // TODO: 装備マネージャーやバフマネージャーから加算値を取得して足す
                float equipmentBonus = 0f;
                float buffBonus = 0f;
                return _playerData.baseMaxLife + equipmentBonus + buffBonus;
            }
        }

        public float CurrentAttackPower
        {
            get
            {
                float equipmentBonus = 0f;
                float buffBonus = 0f;
                return _playerData.baseAttackPower + equipmentBonus + buffBonus;
            }
        }

        public float CurrentDefense
        {
            get
            {
                float equipmentBonus = 0f;
                float buffBonus = 0f;
                return _playerData.baseDefense + equipmentBonus + buffBonus;
            }
        }

        public float CurrentCriticalChance
        {
            get
            {
                float equipmentBonus = 0f;
                float buffBonus = 0f;

                float finalCriticalChance =
                    _playerData.baseCriticalChance + equipmentBonus + buffBonus;

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
                float equipmentBonus = 0f;
                float buffBonus = 0f;
                return _playerData.baseCriticalDamageRatio + equipmentBonus + buffBonus;
            }
        }

        private void Start()
        {
            CurrentHp = CurrentMaxHp;
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
