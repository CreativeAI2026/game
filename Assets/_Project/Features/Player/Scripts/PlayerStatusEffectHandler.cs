using System;
using System.Collections;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの状態異常を管理するコンポーネント。
    /// PlayerStatus と同じ GameObject にアタッチする。
    ///
    /// 【毒（Poison）の仕様】
    /// - ApplyPoison() で毒を付与する。
    /// - 毒中は poisonTickInterval 秒ごとに PlayerStatus.TakeDamage(suppressFlinch: true) を呼ぶ。
    /// - 毒中に再度 ApplyPoison() を呼ぶとタイマーをリセットする（重ね掛けは延長扱い）。
    ///   MidBossDamageArea が定期的に ApplyPoison() を呼ぶことで、
    ///   エリア内にいる限り毒が維持される。
    /// - IsPoisoned プロパティと OnPoisonChanged イベントで UI 等と連携できる。
    /// </summary>
    public class PlayerStatusEffectHandler : MonoBehaviour
    {
        /// <summary>現在毒状態かどうか。</summary>
        public bool IsPoisoned { get; private set; }

        /// <summary>毒状態が変化したときに通知する。引数は新しい IsPoisoned 値。</summary>
        public event Action<bool> OnPoisonChanged;

        private PlayerStatus _playerStatus;
        private Coroutine _poisonCoroutine;

        private float _poisonDurationLeft;
        private float _currentDamagePerTick;
        private float _currentTickInterval;

        private void Awake()
        {
            _playerStatus = GetComponent<PlayerStatus>();
            if (_playerStatus == null)
            {
                Debug.LogError(
                    "[PlayerStatusEffectHandler] PlayerStatus が同一 GameObject に存在しません。"
                );
            }
        }

        /// <summary>
        /// 毒を付与する。毒中に再度呼ばれた場合は持続時間を上書き（延長）し、ダメージ発生タイマーはリセットしない。
        /// </summary>
        public void ApplyPoison(float duration, float damagePerTick, float tickInterval)
        {
            // 残り時間を更新（もし元の残り時間より短ければそのまま、長ければ上書き）
            _poisonDurationLeft = Mathf.Max(_poisonDurationLeft, duration);
            _currentDamagePerTick = damagePerTick;
            _currentTickInterval = tickInterval;

            if (_poisonCoroutine == null)
            {
                _poisonCoroutine = StartCoroutine(PoisonRoutine());
            }
        }

        private IEnumerator PoisonRoutine()
        {
            IsPoisoned = true;
            OnPoisonChanged?.Invoke(true);

            // 毒が付与された瞬間に即座に1回目のダメージが入るように初期値を設定
            float tickTimer = _currentTickInterval;

            // 残り時間がゼロになるまでループ
            while (_poisonDurationLeft > 0f)
            {
                _poisonDurationLeft -= Time.deltaTime;
                tickTimer += Time.deltaTime;

                if (tickTimer >= _currentTickInterval)
                {
                    tickTimer = 0f;
                    if (_playerStatus != null)
                    {
                        // 毒ダメージはひるみを発生させない
                        _playerStatus.TakeDamage(_currentDamagePerTick, false, suppressFlinch: true);
                    }
                }

                yield return null;
            }

            IsPoisoned = false;
            OnPoisonChanged?.Invoke(false);
            _poisonCoroutine = null;
        }
    }
}
