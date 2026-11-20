using System.Collections.Generic;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 直近の一定時間に何回殴られたかを数え、連打されているかを判定する。はじき返しを出すきっかけに使う。
    /// 時刻は呼ぶ側から渡す（EditMode のテストで時刻を進められるようにするため）。
    /// </summary>
    public sealed class TBossMashCounter
    {
        private readonly Queue<float> _hitTimes = new Queue<float>();

        /// <summary>殴られた時刻を記録する。</summary>
        public void Register(float time)
        {
            _hitTimes.Enqueue(time);
        }

        /// <summary>
        /// 今から window 秒以内に hitCount 回以上殴られていれば true。window 秒より古い記録はここで捨てる。
        /// </summary>
        public bool IsMashed(float now, float window, int hitCount)
        {
            while (_hitTimes.Count > 0 && now - _hitTimes.Peek() > window)
            {
                _hitTimes.Dequeue();
            }
            return _hitTimes.Count >= hitCount;
        }

        /// <summary>記録を消す。はじき返した後や、数えない行動の間に呼ぶ。</summary>
        public void Clear()
        {
            _hitTimes.Clear();
        }
    }
}
