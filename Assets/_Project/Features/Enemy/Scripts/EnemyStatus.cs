using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    // 敵のステータスを管理するスクリプト。すべての敵にこれを適用する。
    // 敵のベースパラメータ（体力、攻撃力、防御力）は、EnemyParameterData ScriptableObject で定義する。
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

        // --- 初期化 ---

        void Start()
        {
            // ゲーム開始時、現在HPを計算済みの最大HPで満タンにする
            _currentHp = MaxHp;
        }

        // --- HP変動処理 ---

        public void TakeDamage(float damage, bool isCritical)
        {
            // 防御力によるダメージ軽減計算（最低でも1ダメージは食らうようにする）
            float finalDamage = Mathf.Max(1f, damage - CurrentDefense);

            _currentHp -= finalDamage;

            // ダメージを受けたら常に発見イベントを発火（未発見状態かどうかはコントローラ側で判断）
            OnAlertTriggered?.Invoke();

            // 現在のゲーム内時刻 (Time.time) が、次に怯む許可時刻を過ぎているかチェック
            if (Time.time >= _nextFlinchTime)
            {
                if (isCritical || finalDamage >= _enemyData.flinchDamageThreshold)
                {
                    OnFlinchTriggered?.Invoke();

                    // 現在の時刻にクールダウンを足して、次回のアラーム時刻をセットする
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
            // 回復処理。最大HPを超えないように Mathf.Min で制限する
            _currentHp = Mathf.Min(_currentHp + amount, MaxHp);
            Debug.Log(
                $"{_enemyData.characterName}は {amount} 回復した！ 残りHP: {_currentHp}/{MaxHp}"
            );
        }

        /// <summary>
        /// バフ、デバフが切れた時など、パラメータが変動したタイミングで外部から呼ばれる
        /// </summary>
        public void OnStatusChanged()
        {
            // TODO : 攻撃力、防御力などの変動処理。
        }

        private void Die()
        {
            Debug.Log($"{_enemyData.characterName}は死んだ");
            // ゲームオーバー処理やアニメーション再生など
        }
    }
}
