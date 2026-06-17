using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    // プレイヤーのステータスを管理するスクリプト。
    // プレイヤーのベースパラメータ（体力、攻撃力、防御力）は、PlayerParameterData ScriptableObject で定義する。
    public class PlayerStatus : MonoBehaviour, IDamageable
    {
        [SerializeField]
        private PlayerParameterData _playerData;

        [SerializeField]
        private float _currentHp;

        // HPが変動したときに通知するデリゲート。引数は左から、（現在のHP、最大HP）
        public Action<float, float> OnHpChanged;

        // 演出コンポーネントへの参照（同じ GameObject 上にあることを想定）
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
                float equipmentBonus = 0f; // 例: _equipmentManager.GetMaxHpBonus()
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
            _currentHp = CurrentMaxHp;
            OnHpChanged?.Invoke(_currentHp, CurrentMaxHp);
        }

        public void TakeDamage(float damage, bool isCritical)
        {
            // 防御力によるダメージ軽減計算（最低でも1ダメージは食らうようにする）
            float finalDamage = Mathf.Max(1f, damage - CurrentDefense);

            _currentHp -= finalDamage;
            // UIへ通知
            OnHpChanged?.Invoke(_currentHp, CurrentMaxHp);
            Debug.Log(
                $"プレイヤーは {finalDamage} ダメージを受けた！ 残りHP: {_currentHp}/{CurrentMaxHp}"
            );

            // ─── 演出：被弾時のカメラシェイク・赤ビネット・怯み ───
            CameraShakeManager.Instance?.Shake(0.5f);
            DamageVignette.Instance?.TriggerVignette();
            _flinchHandler?.TriggerFlinch();

            if (_currentHp <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// 攻撃が当たった時に呼び出され、最終的なダメージを算出する
        /// </summary>
        /// <param name="weaponMultiplier">武器固有の倍率（弓なら0.8f、近接なら1.5fなど）</param>
        /// <param name="isCritical">会心が発生したかどうかの結果を返す</param>
        public float RollDamage(float weaponMultiplier, out bool isCritical)
        {
            // （プレイヤーの基礎攻撃力 ＋ 装備等）× 武器の倍率
            float baseDmg = CurrentAttackPower * weaponMultiplier;

            // 会心判定
            isCritical = UnityEngine.Random.Range(0f, 100f) <= CurrentCriticalChance;
            if (isCritical)
            {
                // 攻撃力 + 攻撃力 * 会心上乗せ率
                baseDmg += baseDmg * CurrentCriticalDamageRatio;
            }

            return baseDmg;
        }

        public void Heal(float amount)
        {
            // 回復処理。最大HPを超えないように Mathf.Min で制限する
            _currentHp = Mathf.Min(_currentHp + amount, CurrentMaxHp);
            // UIへ通知
            OnHpChanged?.Invoke(_currentHp, CurrentMaxHp);
            Debug.Log($"プレイヤーは {amount} 回復した！ 残りHP: {_currentHp}/{CurrentMaxHp}");
        }

        /// <summary>
        /// 装備の着脱やバフが切れた時など、最大HPが変動したタイミングで外部から呼ばれる
        /// </summary>
        public void OnStatusChanged()
        {
            // 例：最大HP5000の時にHPが5000あったが、装備を外して最大HPが4000に下がった場合、
            // 現在HPも4000に下げる必要がある。
            if (_currentHp > CurrentMaxHp)
            {
                _currentHp = CurrentMaxHp;
            }

            // UI（HPバー）の更新処理などもここで呼ぶと良い
        }

        private void Die()
        {
            Debug.Log("プレイヤーは力尽きた...");
            // ゲームオーバー処理やアニメーション再生など
        }
    }
}
