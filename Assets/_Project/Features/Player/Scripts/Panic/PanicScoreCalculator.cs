using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 各指標のスコアから、総合焦り度と系統ごとの焦り度（どちらも 0〜100）を算出する。
    ///
    /// 【合算式】確率の「または」（soft-OR）
    ///   焦り度 = 100 × (1 − Π(1 − 重みᵢ × スコアᵢ))
    ///   各指標を「焦っている証拠がどれだけ確からしいか（重み×スコア）」とみなし、
    ///   「どれか1つでも当たっている確率」を焦り度とする。
    ///
    ///   - 1つだけ満点 → 重み×100（逃避 0.7 なら 70）
    ///   - 複数そろうほど 100 に近づくが超えない（逃避と連打が満点 → 1 − 0.3×0.5 = 85）
    ///   - 指標を増やしても式や目盛りを直す必要がない（以前の「平均÷6」は指標数で目盛りが変わった）
    ///
    /// 系統ごとの焦り度も、その系統の指標だけで同じ式を使う。
    /// どちらも、上がる時は速く・下がる時は遅く、目標値へ指数的に追従させる（フレームレートに依存しない）。
    /// </summary>
    [Serializable]
    public class PanicScoreCalculator
    {
        private static readonly PanicCategory[] Categories =
            (PanicCategory[])Enum.GetValues(typeof(PanicCategory));

        [Tooltip("焦り度が上がる時、目標値の約63%まで追いつく時間（秒）。短いほど兆候にすぐ反応する")]
        [SerializeField]
        [Min(0.01f)]
        private float _riseTime = 0.3f;

        [Tooltip("焦り度が下がる時、差の約63%が消えるまでの時間（秒）。長いほど焦りの余韻が残る")]
        [SerializeField]
        [Min(0.01f)]
        private float _fallTime = 4f;

        private readonly float[] _categoryScores = new float[Categories.Length];

        // 系統ごとの「どの指標も当たっていない確率」の積。毎フレーム使い回して確保を避ける
        private readonly float[] _categoryNotPanicProducts = new float[Categories.Length];

        /// <summary>総合焦り度（0〜100）</summary>
        public float Score { get; private set; }

        /// <summary>系統ごとの焦り度が最も高い系統。</summary>
        public PanicCategory DominantCategory { get; private set; }

        /// <summary>系統ごとの焦り度（0〜100）。</summary>
        public float GetCategoryScore(PanicCategory category) => _categoryScores[(int)category];

        /// <summary>各指標の現在値から焦り度を1フレーム分更新する。</summary>
        public void Update(IReadOnlyList<PanicSignal> signals, float deltaTime)
        {
            float notPanicProduct = 1f;
            for (int i = 0; i < _categoryNotPanicProducts.Length; i++)
            {
                _categoryNotPanicProducts[i] = 1f;
            }

            foreach (PanicSignal signal in signals)
            {
                float evidence = Mathf.Clamp01(signal.Weight * signal.Score);
                notPanicProduct *= 1f - evidence;
                _categoryNotPanicProducts[(int)signal.Category] *= 1f - evidence;
            }

            Score = Smooth(Score, (1f - notPanicProduct) * 100f, deltaTime);

            float dominantScore = -1f;
            for (int i = 0; i < _categoryScores.Length; i++)
            {
                _categoryScores[i] = Smooth(
                    _categoryScores[i],
                    (1f - _categoryNotPanicProducts[i]) * 100f,
                    deltaTime
                );
                if (_categoryScores[i] > dominantScore)
                {
                    dominantScore = _categoryScores[i];
                    DominantCategory = Categories[i];
                }
            }
        }

        public void Reset()
        {
            Score = 0f;
            Array.Clear(_categoryScores, 0, _categoryScores.Length);
            DominantCategory = Categories[0];
        }

        private float Smooth(float current, float target, float deltaTime)
        {
            float time = target > current ? _riseTime : _fallTime;
            return current + (target - current) * (1f - Mathf.Exp(-deltaTime / time));
        }
    }
}
