using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 会話の全スキップ(開発用)の状態。オンの間は、既読かどうかに関係なく文字送りと送り待ちを飛ばす
    /// (選択肢は手で選ぶ)。キー判定と表示は Editor の DialogueDebugSkipHotkey が持ち、立てるのもそこだけ
    /// (<c>InternalsVisibleTo</c> は SlotIconView.cs にまとめてある)。ビルドでは常にオフ。
    /// </summary>
    internal static class DialogueDebugSkip
    {
        public static bool Active { get; internal set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Active = false;
    }
}
