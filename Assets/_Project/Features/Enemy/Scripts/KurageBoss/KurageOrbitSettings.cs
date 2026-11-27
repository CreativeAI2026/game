using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスがステージの周りを回る（周回）時の速さと向きの調整値。
    /// </summary>
    [Serializable]
    public sealed class KurageOrbitSettings
    {
        [Tooltip("周回の速さ（m/秒）")]
        [SerializeField]
        private float _speed = 6f;

        [Tooltip("スピードアップ時の周回の速さ（m/秒）")]
        [SerializeField]
        private float _highSpeed = 10f;

        [Tooltip(
            "スピードアップする長さ。その周回の時間に対する割合（0〜1）。段階で周回時間が短くなっても、普通の速さと混ざるよう秒ではなく割合で決める"
        )]
        [SerializeField, Range(0f, 1f)]
        private float _speedUpDurationRatio = 0.4f;

        [Tooltip(
            "周回の速さを変える時の加速度（m/秒²）。スピードアップや切り返しで一瞬で切り替わってカクッと見えないよう、少しずつ変える"
        )]
        [SerializeField]
        private float _acceleration = 20f;

        [Tooltip("周回する時にプレイヤーの方へ回り込む確率。外れると逆へ回る")]
        [SerializeField, Range(0f, 1f)]
        private float _towardPlayerChance = 0.7f;

        /// <summary>周回の速さ（m/秒）。</summary>
        public float Speed => _speed;

        /// <summary>スピードアップ時の周回の速さ（m/秒）。</summary>
        public float HighSpeed => _highSpeed;

        /// <summary>スピードアップする長さの、その周回の時間に対する割合。</summary>
        public float SpeedUpDurationRatio => _speedUpDurationRatio;

        /// <summary>周回の速さを変える時の加速度（m/秒²）。</summary>
        public float Acceleration => _acceleration;

        /// <summary>プレイヤーの方へ回り込む確率。</summary>
        public float TowardPlayerChance => _towardPlayerChance;
    }
}
