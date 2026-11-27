using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// TBoss の特殊攻撃（ワイヤーの触手で捕まえる）の射程・選びやすさ・再使用間隔・当たり判定・各フェーズの長さの調整値。
    /// </summary>
    [Serializable]
    public sealed class TBossSpecialAttackSettings
    {
        [Tooltip("特殊攻撃（触手）の最大射程。")]
        [SerializeField]
        private float _range = 10f;

        [Tooltip("攻撃ステート選択時に特殊攻撃を選ぶ確率（0〜1）。")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _chance = 0.3f;

        [Tooltip("特殊攻撃を選択できるようになるまでの再使用間隔（秒）。")]
        [SerializeField]
        private float _cooldown = 8f;

        [Tooltip(
            "ワイヤー先端の当たり判定の半径（m）。先端の軌跡に沿ってこの太さで掃引し、プレイヤーに触れたら捕獲する。\n"
                + "攻撃前の射線チェックも同じ太さで行う。"
        )]
        [SerializeField]
        private float _wireHitRadius = 1f;

        [Tooltip("特殊攻撃の狙いフェーズの時間（秒）。")]
        [SerializeField]
        private float _aimDuration = 1.5f;

        [Tooltip("特殊攻撃の射出フェーズの時間（秒）。")]
        [SerializeField]
        private float _shootDuration = 0.3f;

        /// <summary>特殊攻撃の最大射程（m）。</summary>
        public float Range => _range;

        /// <summary>攻撃の抽選で特殊攻撃を選ぶ重み。</summary>
        public float Chance => _chance;

        /// <summary>特殊攻撃の再使用間隔（秒）。</summary>
        public float Cooldown => _cooldown;

        /// <summary>ワイヤー先端の当たり判定の半径（m）。</summary>
        public float WireHitRadius => _wireHitRadius;

        /// <summary>狙いフェーズの時間（秒）。</summary>
        public float AimDuration => _aimDuration;

        /// <summary>射出フェーズの時間（秒）。</summary>
        public float ShootDuration => _shootDuration;
    }
}
