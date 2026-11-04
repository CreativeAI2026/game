using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary><see cref="TentacleRope"/> の重さ・しなりの設定。</summary>
    [Serializable]
    public class TentacleRopeSettings
    {
        [Tooltip("重力（m/秒²）。大きいほど触手が垂れて、地面を這うように払う")]
        public float gravity = 6f;

        [Tooltip(
            "勢いの減りやすさ(1/120秒ごとに速さから引く割合)。小さいほど勢いが残り、鞭のように返る"
        )]
        [Range(0f, 0.1f)]
        public float damping = 0.015f;

        [Tooltip(
            "隣り合う部分どうしの速さの差を抑える割合(1/120秒ごと)。全体の振りの勢いは残したまま、細かい振動(カクカク)だけを止める"
        )]
        [Range(0f, 1f)]
        public float internalDamping = 0.15f;

        [Tooltip("根元の曲がりにくさ(バネの硬さ。1/秒²)。大きいほど根元がしっかりする")]
        public float rootBendStiffness = 3000f;

        [Tooltip(
            "先端の曲がりにくさ(バネの硬さ。1/秒²)。小さいほど重さと勢いに負けて、垂れたり遅れたりしなったりする"
        )]
        public float tipBendStiffness = 4f;

        [Tooltip("長さを保つ計算の回数。多いほど伸び縮みしにくくなる")]
        [Min(1)]
        public int iterations = 6;

        [Tooltip("地面に触れた時に浮かせる高さ（m）。触手の太さの半分くらいにする")]
        public float floorOffset = 0.3f;

        [Tooltip("ポータルの穴の半径（m）。地面より下へはこの中からしか入れない")]
        public float holeRadius = 1.6f;
    }

    /// <summary>
    /// 触手を、重さとしなりを持つ1本の紐として動かす(位置ベースの物理)。
    /// 外から動かすのは根元の位置と向きだけで、その先は慣性・重力・曲がりにくさで決まる。
    /// そのため、振れば先端が遅れて付いてきて、速く振るほど遠心力で伸び、根元が止まれば先端が追い越して巻き込む。
    /// 曲がりにくさは根元ほど強く先端ほど弱いので、根元はしっかり、先端はよくしなる。
    /// 地面は床として扱い、地面より下へはポータルの穴からしか入れない(穴の外でめり込まず、戻る時は穴へ引き込まれる)。
    /// </summary>
    public class TentacleRope
    {
        // フレームが重くても紐が暴れないよう、この間隔に分けて進める
        private const float MaxStep = 1f / 120f;

        // 穴を漏斗の形にする深さ（m）。地面すれすれでは穴の縁まで通れて、この深さで中心へ寄り切る。
        // 沈むほど少しずつ中心へ寄るので、急に寄せて継ぎ目が跳ねることがない
        private const float HoleFunnelDepth = 2f;

        private readonly Transform[] _bones;
        private readonly float[] _lengths;
        private readonly Vector3[] _positions;
        private readonly Vector3[] _previous;

        public TentacleRope(Transform[] bones)
        {
            _bones = bones;
            int count = bones.Length;
            _lengths = new float[count - 1];
            for (int i = 0; i < count - 1; i++)
                _lengths[i] = Vector3.Distance(bones[i].position, bones[i + 1].position);
            _positions = new Vector3[count];
            _previous = new Vector3[count];
        }

        /// <summary>
        /// 今のボーンの位置から動かし始める。previousPositions に1フレーム前の位置を渡すと、それまでの動きの勢いを引き継ぐ。
        /// </summary>
        public void Begin(Vector3[] previousPositions, float previousDeltaTime)
        {
            for (int i = 0; i < _bones.Length; i++)
            {
                _positions[i] = _bones[i].position;
                // 1フレームぶんの移動を、計算の1段ぶんの移動に直して引き継ぐ
                Vector3 step =
                    (_positions[i] - previousPositions[i])
                    * (MaxStep / Mathf.Max(previousDeltaTime, MaxStep));
                _previous[i] = _positions[i] - step;
            }
        }

        /// <summary>
        /// 紐を進める。根元は bones[0] の今の位置に置き、根元の部分は rootDirection へ向ける。
        /// </summary>
        /// <param name="tension">力の入れ具合。曲がりにくさに掛ける倍率で、先端ほど強く効く(力を入れると先までピンと張る)</param>
        /// <param name="muscleDirection">i 番目の継ぎ目で、手前の向き(引数)に対して次の部分を向けたい向き(筋肉で曲げようとする形)。手前の向きをそのまま返せば真っすぐ</param>
        public void Step(
            float deltaTime,
            TentacleRopeSettings settings,
            Vector3 rootDirection,
            Vector3 holeCenter,
            Func<int, Vector3, Vector3> muscleDirection,
            float tension = 1f
        )
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(deltaTime / MaxStep));
            float dt = deltaTime / steps;
            Vector3 gravityStep = Vector3.down * (settings.gravity * dt * dt);
            Vector3 root = _bones[0].position;
            int count = _positions.Length;

            for (int s = 0; s < steps; s++)
            {
                // 慣性(前の動きを続ける)と重力。手前の部分との速さの差は抑え、細かく震えないようにする
                Vector3 parentVelocity = Vector3.zero;
                for (int i = 1; i < count; i++)
                {
                    Vector3 velocity = (_positions[i] - _previous[i]) * (1f - settings.damping);
                    velocity -= (velocity - parentVelocity) * settings.internalDamping;
                    parentVelocity = velocity;
                    _previous[i] = _positions[i];
                    _positions[i] += velocity + gravityStep;
                }
                _positions[0] = root;
                _previous[0] = root;

                // 根元の部分は向きを外から決める
                _positions[1] = root + rootDirection * _lengths[0];

                // 曲がりにくさ: 手前の向きを筋肉の曲げだけ回した向きへ、次の点を寄せる。
                // 寄せる量はバネの硬さ×時間²(重力や勢いと同じく力として働かせ、刻み方で硬さが変わらないように)。根元ほど硬く、先端ほど柔らかい
                for (int i = 1; i < count - 1; i++)
                {
                    Vector3 parentDirection = (_positions[i] - _positions[i - 1]).normalized;
                    Vector3 desired =
                        _positions[i] + muscleDirection(i, parentDirection) * _lengths[i];
                    float along = count > 2 ? (float)(i - 1) / (count - 3) : 1f;
                    float stiffness =
                        settings.rootBendStiffness
                        * Mathf.Pow(settings.tipBendStiffness / settings.rootBendStiffness, along)
                        * Mathf.Lerp(1f, tension, along);
                    _positions[i + 1] +=
                        (desired - _positions[i + 1]) * Mathf.Min(1f, stiffness * dt * dt);
                }

                for (int it = 0; it < settings.iterations; it++)
                {
                    _positions[1] = root + rootDirection * _lengths[0];

                    // 長さを保つ(根元から順に、先の点だけを動かす)
                    for (int i = 0; i < count - 1; i++)
                    {
                        Vector3 segment = _positions[i + 1] - _positions[i];
                        float length = segment.magnitude;
                        if (length > 1e-6f)
                            _positions[i + 1] = _positions[i] + segment * (_lengths[i] / length);
                    }

                    for (int i = 1; i < count; i++)
                        ResolveGround(i, settings, holeCenter);
                }
            }
        }

        /// <summary>計算した紐の形をボーンへ書き込む。ボーンは前もって元の姿勢に戻しておくこと。</summary>
        public void WriteToBones()
        {
            for (int i = 0; i < _bones.Length - 1; i++)
            {
                Vector3 current = _bones[i + 1].position - _bones[i].position;
                Vector3 desired = _positions[i + 1] - _positions[i];
                _bones[i].rotation =
                    Quaternion.FromToRotation(current, desired) * _bones[i].rotation;
            }
        }

        /// <summary>地面は床として押し返す。ポータルの穴の上だけは下へ通し、地面より下では穴の中心へ寄せる。</summary>
        private void ResolveGround(int index, TentacleRopeSettings settings, Vector3 holeCenter)
        {
            Vector3 position = _positions[index];
            float floor = holeCenter.y + settings.floorOffset;
            if (position.y >= floor)
                return;

            Vector3 fromHole = position - holeCenter;
            fromHole.y = 0f;
            float distance = fromHole.magnitude;

            if (position.y > holeCenter.y && distance > settings.holeRadius)
            {
                // 穴の外: 床の上に乗せ、下向きの勢いを消す(横へは滑れる)
                position.y = floor;
                _previous[index].y = floor;
            }
            else if (position.y <= holeCenter.y)
            {
                // 地面より下: 深さに応じて細くなる漏斗の中に収める
                float depth = holeCenter.y - position.y;
                float allowed = settings.holeRadius * Mathf.Clamp01(1f - depth / HoleFunnelDepth);
                if (distance > allowed)
                    position -= fromHole * (1f - allowed / distance);
            }
            _positions[index] = position;
        }
    }
}
