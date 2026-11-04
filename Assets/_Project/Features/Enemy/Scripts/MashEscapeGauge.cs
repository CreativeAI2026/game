using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 拘束から抜け出すレバガチャのゲージ。移動入力の向きが変わった回数でゲージを溜める。
    /// TutorialBoss の掴みとクラゲボスの輪っかで共通に使う(演出や拘束の仕方は敵ごとに違うが、入力の数え方は揃える)。
    /// </summary>
    public class MashEscapeGauge
    {
        private float _perInput;
        private Vector2 _prevMoveInput;

        public float Value { get; private set; }
        public float Threshold { get; private set; }
        public bool IsFull => Value >= Threshold;

        /// <summary>拘束の開始時に呼ぶ。ゲージを空にし、前回の入力の記録を捨てる。</summary>
        public void Reset(float threshold, float perInput)
        {
            Threshold = threshold;
            _perInput = perInput;
            Value = 0f;
            _prevMoveInput = Vector2.zero;
        }

        /// <summary>
        /// 毎フレームの移動入力を渡す。入力の向きが変わってゲージが増えたら true。
        /// </summary>
        public bool Feed(Vector2 currentMove)
        {
            bool changed =
                HasDirectionChanged(_prevMoveInput.x, currentMove.x)
                || HasDirectionChanged(_prevMoveInput.y, currentMove.y);
            _prevMoveInput = currentMove;

            if (!changed)
                return false;

            Value = Mathf.Min(Value + _perInput, Threshold);
            return true;
        }

        private static bool HasDirectionChanged(float prev, float current)
        {
            if (Mathf.Approximately(prev, 0f) && Mathf.Approximately(current, 0f))
                return false;
            if (Mathf.Approximately(prev, 0f) && !Mathf.Approximately(current, 0f))
                return true;
            if (prev > 0f && current < 0f)
                return true;
            if (prev < 0f && current > 0f)
                return true;
            return false;
        }
    }
}
