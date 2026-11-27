using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// TBoss の通常攻撃2（腕を伸ばした鎌）の射程・選びやすさ・再使用間隔・狙いと引き寄せの調整値。
    /// </summary>
    [Serializable]
    public sealed class TBossNormalAttack2Settings
    {
        [Tooltip("通常攻撃2（腕伸ばし）の判定射程。")]
        [SerializeField]
        private float _range = 4f;

        [Tooltip("攻撃ステート選択時に通常攻撃2を選ぶ確率（0〜1）。特殊攻撃が外れた後に判定する。")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _chance = 0.4f;

        [Tooltip("通常攻撃2を選択できるようになるまでの再使用間隔（秒）。")]
        [SerializeField]
        private float _cooldown = 2f;

        [Tooltip(
            "通常攻撃2で、振りに入る前にプレイヤーへ向き直る速度。0で向き直らない。\n"
                + "腕を伸ばし始めた時点（OnAttack2ReachExtend）で向きを固定するため、\n"
                + "振りに入った後の回避は可能なまま、振りの前だけ狙いを付け直す。"
        )]
        [SerializeField]
        private float _homingRotateSpeed = 6f;

        [Tooltip(
            "ヒット時にプレイヤーを引き寄せる距離（メートル）。理不尽な強制引き寄せにならないよう小さめに。"
        )]
        [SerializeField]
        private float _pullDistance = 0.6f;

        [Tooltip("ヒット時の引き寄せにかける時間（秒）。")]
        [SerializeField]
        private float _pullDuration = 0.15f;

        /// <summary>通常攻撃2の判定射程（m）。</summary>
        public float Range => _range;

        /// <summary>攻撃の抽選で通常攻撃2を選ぶ重み。</summary>
        public float Chance => _chance;

        /// <summary>通常攻撃2の再使用間隔（秒）。</summary>
        public float Cooldown => _cooldown;

        /// <summary>振りに入る前にプレイヤーへ向き直る速度。</summary>
        public float HomingRotateSpeed => _homingRotateSpeed;

        /// <summary>ヒット時にプレイヤーを引き寄せる距離（m）。</summary>
        public float PullDistance => _pullDistance;

        /// <summary>ヒット時の引き寄せにかける時間（秒）。</summary>
        public float PullDuration => _pullDuration;
    }
}
