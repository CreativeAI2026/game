using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 防御メーターの調整値。Inspector で剣(SwordController)に持たせる。
    /// </summary>
    [Serializable]
    public sealed class GuardGaugeSettings
    {
        [Tooltip("メーターの最大量。敵の攻撃のダメージ値と同じ単位で減る")]
        [SerializeField]
        private float _max = 1500f;

        [Tooltip("受けたダメージ 1 あたりのメーター消費量。大きいほど早く割れる")]
        [SerializeField]
        private float _costPerDamage = 1f;

        [Tooltip("1 秒あたりの回復量。防御を構えている間は回復しない")]
        [SerializeField]
        private float _regenPerSecond = 150f;

        [Tooltip("最後に防御で受けてから回復が始まるまでの時間（秒）")]
        [SerializeField]
        private float _regenDelay = 1.5f;

        [Tooltip("割れた後、再び防御できるようになるメーターの割合（0〜1）")]
        [Range(0f, 1f)]
        [SerializeField]
        private float _recoverRatioAfterBreak = 0.5f;

        public GuardGaugeSettings() { }

        public GuardGaugeSettings(
            float max,
            float costPerDamage,
            float regenPerSecond,
            float regenDelay,
            float recoverRatioAfterBreak
        )
        {
            _max = max;
            _costPerDamage = costPerDamage;
            _regenPerSecond = regenPerSecond;
            _regenDelay = regenDelay;
            _recoverRatioAfterBreak = recoverRatioAfterBreak;
        }

        /// <summary>メーターの最大量。</summary>
        public float Max => _max;

        /// <summary>受けたダメージ 1 あたりのメーター消費量。</summary>
        public float CostPerDamage => _costPerDamage;

        /// <summary>1 秒あたりの回復量。</summary>
        public float RegenPerSecond => _regenPerSecond;

        /// <summary>最後に防御で受けてから回復が始まるまでの時間（秒）。</summary>
        public float RegenDelay => _regenDelay;

        /// <summary>割れた後、再び防御できるようになるメーターの割合（0〜1）。</summary>
        public float RecoverRatioAfterBreak => _recoverRatioAfterBreak;
    }

    /// <summary>
    /// 剣の防御で攻撃を受けるたびに減り、時間で回復する防御メーター。0 になると割れる。
    ///
    /// 防御を解いても量は残るので、小刻みに構え直しても受けられる量は増えない。
    /// 毎フレームの更新を持たず、渡された時刻から回復量を計算する。
    /// 剣を持ち替えて剣のオブジェクトが止まっている間も回復を進めるため。
    /// </summary>
    public sealed class GuardGauge
    {
        private readonly GuardGaugeSettings _settings;

        private float _value;

        // _value がどの時刻の量か
        private float _settledTime;

        // この時刻より前は回復しない(防御で受けるたびに先へ延びる)
        private float _regenStartTime;
        private bool _isGuarding;
        private bool _isBroken;

        public GuardGauge(GuardGaugeSettings settings)
        {
            _settings = settings;
            _value = settings.Max;
        }

        /// <summary>メーターの最大量。</summary>
        public float Max => _settings.Max;

        /// <summary>指定した時刻の残量。</summary>
        public float GetCurrent(float now)
        {
            Settle(now);
            return _value;
        }

        /// <summary>割れた後、再び防御できる量までまだ戻っていないか。</summary>
        public bool IsBroken(float now)
        {
            Settle(now);
            return _isBroken;
        }

        /// <summary>防御を構え始めた。構えている間は回復が止まる。</summary>
        public void BeginGuard(float now)
        {
            Settle(now);
            _isGuarding = true;
        }

        /// <summary>防御を解いた。</summary>
        public void EndGuard(float now)
        {
            Settle(now);
            _isGuarding = false;
        }

        /// <summary>
        /// 防御で攻撃を受け、ダメージに応じてメーターを減らす。
        /// </summary>
        /// <returns>この攻撃でメーターが尽きて割れたら true</returns>
        public bool Absorb(float damage, float now)
        {
            Settle(now);

            _value -= Mathf.Max(0f, damage) * _settings.CostPerDamage;
            _regenStartTime = now + _settings.RegenDelay;

            if (_value > 0f)
                return false;

            _value = 0f;
            _isBroken = true;
            return true;
        }

        /// <summary>前回の計算から now までの回復を反映する。</summary>
        private void Settle(float now)
        {
            if (!_isGuarding)
            {
                float regenFrom = Mathf.Max(_settledTime, _regenStartTime);
                if (now > regenFrom)
                    _value += (now - regenFrom) * _settings.RegenPerSecond;
            }

            // 回復分のほか、プレイ中に最大量を下げた場合もここで収める
            _value = Mathf.Min(_value, _settings.Max);
            _settledTime = Mathf.Max(_settledTime, now);

            if (_isBroken && _value >= _settings.Max * _settings.RecoverRatioAfterBreak)
                _isBroken = false;
        }
    }
}
