using System;
using System.Collections;
using System.Collections.Generic;

namespace CreativeAI.Tests
{
    /// <summary>
    /// コルーチン(IEnumerator)をフレームを待たずにその場で回すヘルパ。
    /// yield return した入れ子の IEnumerator も Unity と同じように中へ入って回す。
    /// </summary>
    public static class TestCoroutine
    {
        /// <summary>最後まで回す。</summary>
        public static void Drive(IEnumerator routine) => DriveUntil(routine, () => false);

        /// <summary>
        /// <paramref name="stop"/> が true になるか、最後まで進むまで回す。
        /// </summary>
        public static void DriveUntil(IEnumerator routine, Func<bool> stop)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0 && !stop())
            {
                var current = stack.Peek();
                if (!current.MoveNext())
                    stack.Pop();
                else if (current.Current is IEnumerator nested)
                    stack.Push(nested);
            }
        }
    }
}
