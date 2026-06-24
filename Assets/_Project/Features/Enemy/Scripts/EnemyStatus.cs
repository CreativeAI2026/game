using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 全敵共通のステータス管理。ベースパラメータはEnemyParameterData ScriptableObjectで定義し、
    /// ランタイムではバフ・デバフによる変動分を加算する設計。
    /// </summary>
    public class EnemyStatus : MonoBehaviour, IDamageable
    {
        [SerializeField]
        private EnemyParameterData _enemyData;

        [SerializeField]
        private float _currentHp;

        private float _nextFlinchTime = 0f;
        public event Action OnFlinchTriggered;
        public event Action OnAlertTriggered;
        public event Action OnDeathTriggered;
        public float MaxHp => _enemyData.baseMaxLife;

        public float CurrentAttackPower
        {
            get
            {
                float buffBonus = 0f;
                return _enemyData.baseAttackPower + buffBonus;
            }
        }

        public float CurrentDefense
        {
            get
            {
                float buffBonus = 0f;
                return _enemyData.baseDefense + buffBonus;
            }
        }

        void Start()
        {
            _currentHp = MaxHp;
        }

        public void TakeDamage(float damage, bool isCritical)
        {
            // プレイヤーの攻撃が完全に無効化され、進行不能やフィードバック喪失に陥るのを防ぐための保証値
            float finalDamage = Mathf.Max(1f, damage - CurrentDefense);

            _currentHp -= finalDamage;

            // StatusクラスがAIの現在の状態（未発見等）に依存するのを防ぎ、状態管理の責務をコントローラ側に集約するため、イベントは無条件で発火させる
            OnAlertTriggered?.Invoke();

            // 連続攻撃によるハメ状態（永続的な怯み）を防止し、反撃の機会を確保するためのスーパーアーマー設計
            if (Time.time >= _nextFlinchTime)
            {
                if (isCritical || finalDamage >= _enemyData.flinchDamageThreshold)
                {
                    OnFlinchTriggered?.Invoke();

                    _nextFlinchTime = Time.time + _enemyData.flinchCooldownTime;
                    Debug.Log(
                        $"{_enemyData.characterName}が怯んだ！ {_enemyData.flinchCooldownTime}秒間スーパーアーマーになります。"
                    );
                }
            }
            Debug.Log(
                $"{_enemyData.characterName}は {finalDamage} ダメージを受けた！ 残りHP: {_currentHp}/{MaxHp}"
            );

            if (_currentHp <= 0)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            _currentHp = Mathf.Min(_currentHp + amount, MaxHp);
            Debug.Log(
                $"{_enemyData.characterName}は {amount} 回復した！ 残りHP: {_currentHp}/{MaxHp}"
            );
        }

        // バフ・デバフの適用タイミングで外部から呼び出し、パラメータ再計算のフックとする
        public void OnStatusChanged()
        {
            // TODO : 攻撃力、防御力などの変動処理。
        }

        private void Die()
        {
            OnDeathTriggered?.Invoke();
            Debug.Log($"{_enemyData.characterName}は死んだ");
            Destroy(gameObject, 5.0f);
        }
    }
}
