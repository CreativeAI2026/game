using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの焦りに応じたボスの追い込みの設定。TutorialBossController が Inspector に出して持つ。
    /// 判断そのものは <see cref="TBossPanicPressure"/> が行う。
    /// </summary>
    [Serializable]
    public class TBossPanicPressureSettings
    {
        [Header("動揺（軽い追い込み）")]
        [Tooltip("動揺している相手への攻撃間隔の倍率")]
        [SerializeField]
        [Min(0.05f)]
        private float _tenseAttackIntervalScale = 0.8f;

        [Tooltip("動揺している相手に、様子見を挟まず突っ込む確率へ足す値")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _tenseDirectAssaultBonus = 0.15f;

        // 各系統の初期値の考え方は設計（段階3）に合わせる:
        //   逃げ腰   … 逃げる相手を触手で引き戻し、突っ込み、見失ってもしつこく追う
        //   硬直     … 固まった相手に出の速い通常攻撃を間を置かず畳みかける
        //   防御崩れ … 連打・押しっぱなしのタイミングを、防げない触手とためのある鎌で外す
        //   迷い     … 弓を構えられても下がらず内側まで詰め、迷う時間を与えない
        [Header("焦り（系統ごとの追い込み）")]
        [Tooltip("逃げ腰（逃避・空間喪失）の相手への追い込み方")]
        [SerializeField]
        private TBossPressureProfile _avoidance = new TBossPressureProfile(
            attackIntervalScale: 0.7f,
            directAssaultChance: 0.9f,
            normalWeightScale: 1f,
            normal2WeightScale: 1f,
            specialWeightScale: 3f,
            closesIn: false,
            lostSightDurationScale: 1.5f
        );

        [Tooltip("防御崩れ（防御の連打・連続被弾）の相手への追い込み方")]
        [SerializeField]
        private TBossPressureProfile _defenseBreakdown = new TBossPressureProfile(
            attackIntervalScale: 0.7f,
            directAssaultChance: 0.6f,
            normalWeightScale: 0.5f,
            normal2WeightScale: 2f,
            specialWeightScale: 3f,
            closesIn: false,
            lostSightDurationScale: 1f
        );

        [Tooltip("迷い（弓のキャンセル・照準のぶれ・武器切替）の相手への追い込み方")]
        [SerializeField]
        private TBossPressureProfile _indecision = new TBossPressureProfile(
            attackIntervalScale: 0.6f,
            directAssaultChance: 0.8f,
            normalWeightScale: 1.5f,
            normal2WeightScale: 1f,
            specialWeightScale: 1f,
            closesIn: true,
            lostSightDurationScale: 1f
        );

        [Tooltip("硬直（攻撃が来ても何もできない）の相手への追い込み方")]
        [SerializeField]
        private TBossPressureProfile _freeze = new TBossPressureProfile(
            attackIntervalScale: 0.4f,
            directAssaultChance: 0.8f,
            normalWeightScale: 2f,
            normal2WeightScale: 0.7f,
            specialWeightScale: 0.7f,
            closesIn: true,
            lostSightDurationScale: 1f
        );

        [Header("歯止め（ハメにしない）")]
        [Tooltip("焦りの追い込みを続けられる最大時間（秒）。超えたら息継ぎに入る")]
        [SerializeField]
        [Min(0.1f)]
        private float _maxPressureSeconds = 8f;

        [Tooltip("追い込み中にボスの攻撃がこの回数当たったら、時間が残っていても息継ぎに入る")]
        [SerializeField]
        [Min(1)]
        private int _maxHitsPerPressure = 2;

        [Tooltip("息継ぎの長さ（秒）。この間は追い込まない")]
        [SerializeField]
        [Min(0f)]
        private float _breatherSeconds = 4f;

        [Tooltip("息継ぎ中の攻撃間隔の倍率。1より大きくして、立て直す時間を与える")]
        [SerializeField]
        [Min(0.05f)]
        private float _breatherAttackIntervalScale = 1.2f;

        [Header("合図")]
        [Tooltip(
            "追い込み開始の合図（目の光と音）から、最初の追い込み攻撃までに最低限あける時間（秒）"
        )]
        [SerializeField]
        [Min(0f)]
        private float _cueLeadSeconds = 0.5f;

        public float TenseAttackIntervalScale => _tenseAttackIntervalScale;
        public float TenseDirectAssaultBonus => _tenseDirectAssaultBonus;
        public float MaxPressureSeconds => _maxPressureSeconds;
        public int MaxHitsPerPressure => _maxHitsPerPressure;
        public float BreatherSeconds => _breatherSeconds;
        public float BreatherAttackIntervalScale => _breatherAttackIntervalScale;
        public float CueLeadSeconds => _cueLeadSeconds;

        /// <summary>焦りの系統に対応する追い込み方。</summary>
        public TBossPressureProfile GetProfile(PanicCategory category) =>
            category switch
            {
                PanicCategory.Avoidance => _avoidance,
                PanicCategory.DefenseBreakdown => _defenseBreakdown,
                PanicCategory.Indecision => _indecision,
                PanicCategory.Freeze => _freeze,
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
            };
    }
}
