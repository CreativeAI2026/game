using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 各指標のスコアから総合焦り度（0〜100）を算出する。
    ///
    /// 【設計方針】
    /// 「1つの行動を強く繰り返す」こと自体も焦りの強いシグナルであるため、
    ///  単純な重み付き合計ではなく「最大値主体 + 多様性ボーナス」の式を採用する。
    ///
    ///  PanicScore = max(各指標スコア) × 70  ← 1行動の強度を主軸にする
    ///             + avg(全指標スコア) × 30  ← 複数行動の組み合わせでさらに上昇
    ///
    /// 結果:
    ///   1指標だけ 100%  → 約 75（6指標の場合。明確な焦り状態と判定）
    ///   全指標が  100%  → 100  （極度の焦り）
    ///
    /// 毎フレーム自然減衰も適用する。
    /// </summary>
    [Serializable]
    public class PanicScoreCalculator
    {
        // 最大値の項と平均の項の配分（合計100）
        private const float MaxTermWeight = 70f;
        private const float AverageTermWeight = 30f;

        // 上昇時に目標値へ追いつく速さ（毎秒、差の何割を詰めるか）
        private const float RiseRate = 5f;

        [Tooltip("焦り度が毎秒どれだけ自然に減少するか")]
        [SerializeField]
        private float _decayPerSecond = 5f;

        /// <summary>総合焦り度（0〜100）</summary>
        public float Score { get; private set; }

        /// <summary>各指標の現在値から総合焦り度を1フレーム分更新する。</summary>
        public void Update(IReadOnlyList<PanicSignal> signals, float deltaTime)
        {
            float maxScore = 0f;
            float sum = 0f;
            foreach (PanicSignal signal in signals)
            {
                float s = signal.Score;
                maxScore = Mathf.Max(maxScore, s);
                sum += s;
            }
            float averageScore = signals.Count > 0 ? sum / signals.Count : 0f;

            float raw = maxScore * MaxTermWeight + averageScore * AverageTermWeight;

            // 上昇は素早く追いつき、自然減衰はゆっくり
            if (raw > Score)
            {
                Score = Mathf.Lerp(Score, raw, deltaTime * RiseRate);
            }
            else
            {
                Score = Mathf.Max(0f, Score - _decayPerSecond * deltaTime);
            }

            Score = Mathf.Clamp(Score, 0f, 100f);
        }

        public void Reset()
        {
            Score = 0f;
        }
    }
}
