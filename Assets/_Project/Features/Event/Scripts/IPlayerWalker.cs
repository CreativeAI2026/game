using UnityEngine;

namespace CreativeAI.Core
{
    /// <summary>
    /// 入力を使わず、目標の位置まで歩いて向かえるプレイヤー。
    /// EventPlayer(Core)がプレイヤーの実装(Gameplay)を知らずに、引き戻し(player.walkTo)を頼むための窓口。
    /// </summary>
    public interface IPlayerWalker
    {
        /// <summary>歩く速さ(m/s)。到着までの目安時間の計算に使う。</summary>
        float WalkSpeed { get; }

        /// <summary>歩いて向かっている最中か。着くか <see cref="StopWalking"/> で false に戻る。</summary>
        bool IsWalking { get; }

        /// <summary>目標の位置まで歩いて向かう。</summary>
        void WalkTo(Vector3 worldPosition);

        /// <summary>歩いて向かう指示をやめる(その場で止まる)。</summary>
        void StopWalking();
    }
}
