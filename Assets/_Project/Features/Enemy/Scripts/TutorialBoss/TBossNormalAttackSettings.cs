using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// TBoss の通常攻撃（踏み込んで振る）の射程・再使用間隔・踏み込みの調整値。
    /// </summary>
    [Serializable]
    public sealed class TBossNormalAttackSettings
    {
        [Tooltip("通常攻撃の攻撃射程。")]
        [SerializeField]
        private float _range = 2f;

        [Tooltip("通常攻撃を選択できるようになるまでの再使用間隔（秒）。")]
        [SerializeField]
        private float _cooldown = 1f;

        [Tooltip(
            "踏み込み後に、プレイヤーの中心がボスの中心からこの距離に来るよう踏み込みの長さを合わせる（m）。\n"
                + "クリップの振りで当たり判定（半径1m）の中心が最も前に出るのは約3.0m先のため、その少し外を狙う。"
        )]
        [SerializeField]
        private float _strikeDistance = 3.5f;

        [Tooltip(
            "踏み込みで動かす長さの上限（m）。踏み込み中も後ろ足は接地したままなので、長くすると後ろ足が引きずられて見える。"
        )]
        [SerializeField]
        private float _maxStepIn = 1.5f;

        /// <summary>通常攻撃の攻撃射程（m）。</summary>
        public float Range => _range;

        /// <summary>通常攻撃の再使用間隔（秒）。</summary>
        public float Cooldown => _cooldown;

        /// <summary>踏み込み後にプレイヤーの中心が来るよう合わせる、中心間の距離（m）。</summary>
        public float StrikeDistance => _strikeDistance;

        /// <summary>踏み込みで動かす長さの上限（m）。</summary>
        public float MaxStepIn => _maxStepIn;
    }
}
