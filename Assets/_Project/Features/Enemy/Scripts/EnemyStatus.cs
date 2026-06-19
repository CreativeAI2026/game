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
            // 防御力が攻撃力を上回ってもダメージが0にならないよう、最低1ダメージを保証する
            float finalDamage = Mathf.Max(1f, damage - CurrentDefense);

            _currentHp -= finalDamage;

            // 被弾=敵に気づかれるべきなので、発見状態かどうかに関係なく常にイベントを発火する。
            // 未発見状態かどうかの判断はコントローラ側の責務とする。
            OnAlertTriggered?.Invoke();

            // クールダウン中は怯みを発生させない（スーパーアーマー期間）。
            // 連続攻撃でハメ状態になるのを防ぐための設計。
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
            Debug.Log($"{_enemyData.characterName}は死んだ");
            // TODO : ゲームオーバー処理やアニメーション再生など
        }
    }
}
