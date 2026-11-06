using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.PlayMode
{
    /// <summary>
    /// 「何秒待てば終わっているはず」で待つと、CI の遅いマシンで落ちる。
    /// 条件が満たされるまでフレームを進め、上限を超えたら理由付きで落とす。
    /// </summary>
    internal static class PlayModeWait
    {
        public const float DefaultTimeout = 10f;

        public static IEnumerator Until(
            Func<bool> condition,
            string message,
            float timeout = DefaultTimeout
        )
        {
            float limit = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > limit)
                    Assert.Fail($"{timeout} 秒待っても満たされない: {message}");
                yield return null;
            }
        }

        /// <summary>dirty フラグを LateUpdate でまとめて反映する UI を待つ。</summary>
        public static IEnumerator Frames(int count = 3)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }
    }
}
