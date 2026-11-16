using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Core
{
    /// <summary>
    /// 本体シーンの読み込みにつられて始まった、追加の非同期読み込み(重ねる小物シーンなど)を集める窓口。
    /// <see cref="SceneController"/> は本体を有効化したあと、ここに登録された読み込みが全部終わるまで暗転を明けない。
    /// 読み込みを始める側(AdditiveScenes)がここへ登録する。
    /// </summary>
    public static class PendingSceneLoads
    {
        // 進み具合(0..1、1で完了)を返す関数の一覧。AsyncOperation 以外(テストの偽物)も載せられるよう関数で持つ。
        private static readonly List<Func<float>> s_loads = new();

        public static void Register(AsyncOperation op)
        {
            if (op != null)
                Register(() => op.isDone ? 1f : op.progress);
        }

        /// <param name="progress01">進み具合。1 を返したら完了とみなす。</param>
        public static void Register(Func<float> progress01)
        {
            if (progress01 != null)
                s_loads.Add(progress01);
        }

        public static int Count => s_loads.Count;

        public static bool AllDone => Progress01 >= 1f;

        /// <summary>登録された読み込みの進み具合の平均(何も無ければ 1)。</summary>
        public static float Progress01
        {
            get
            {
                if (s_loads.Count == 0)
                    return 1f;
                float sum = 0f;
                foreach (var load in s_loads)
                    sum += Mathf.Clamp01(load());
                return sum / s_loads.Count;
            }
        }

        // ドメインリロード無しで Play を繰り返したときに、前回の読み込みが残らないようにする
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => s_loads.Clear();
    }
}
