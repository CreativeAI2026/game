using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 焦りの系統1つに対する、ボスの追い込み方（各判断に掛ける補正値）。
    /// TBossPanicPressureSettings が系統ごとに1つずつ持つ。
    /// </summary>
    [Serializable]
    public class TBossPressureProfile
    {
        [Tooltip("攻撃と攻撃の間隔に掛ける倍率。小さいほど畳みかける")]
        [SerializeField]
        [Min(0.05f)]
        private float _attackIntervalScale = 1f;

        [Tooltip("追跡で間合いに入った時、様子見を挟まず突っ込む確率の下限（0〜1）。元の確率がこれより高ければそちらを使う")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _directAssaultChance;

        [Tooltip("通常攻撃（出の速い近接）の選ばれやすさに掛ける倍率")]
        [SerializeField]
        [Min(0f)]
        private float _normalWeightScale = 1f;

        [Tooltip("通常攻撃2（ためのある鎌）の選ばれやすさに掛ける倍率")]
        [SerializeField]
        [Min(0f)]
        private float _normal2WeightScale = 1f;

        [Tooltip("特殊攻撃（防げない触手の掴み）の選ばれやすさに掛ける倍率")]
        [SerializeField]
        [Min(0f)]
        private float _specialWeightScale = 1f;

        [Tooltip("様子見の間合いを内側まで詰めるか。弓を構えられても下がらない")]
        [SerializeField]
        private bool _closesIn;

        [Tooltip("プレイヤーを見失ってから諦めるまでの時間に掛ける倍率。大きいほどしつこく追う")]
        [SerializeField]
        [Min(0.1f)]
        private float _lostSightDurationScale = 1f;

        // Unity のシリアライズ用（Inspector で値が未保存の時は各フィールドの初期値になる）
        public TBossPressureProfile() { }

        public TBossPressureProfile(
            float attackIntervalScale,
            float directAssaultChance,
            float normalWeightScale,
            float normal2WeightScale,
            float specialWeightScale,
            bool closesIn,
            float lostSightDurationScale
        )
        {
            _attackIntervalScale = attackIntervalScale;
            _directAssaultChance = directAssaultChance;
            _normalWeightScale = normalWeightScale;
            _normal2WeightScale = normal2WeightScale;
            _specialWeightScale = specialWeightScale;
            _closesIn = closesIn;
            _lostSightDurationScale = lostSightDurationScale;
        }

        public float AttackIntervalScale => _attackIntervalScale;
        public float DirectAssaultChance => _directAssaultChance;
        public float NormalWeightScale => _normalWeightScale;
        public float Normal2WeightScale => _normal2WeightScale;
        public float SpecialWeightScale => _specialWeightScale;
        public bool ClosesIn => _closesIn;
        public float LostSightDurationScale => _lostSightDurationScale;
    }
}
