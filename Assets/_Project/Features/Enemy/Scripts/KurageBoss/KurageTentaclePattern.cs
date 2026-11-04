using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>地面の触手1本の出し方。</summary>
    public readonly struct KurageTentacleSpawn
    {
        /// <summary>出現位置(ステージ上面)。</summary>
        public readonly Vector3 Position;

        /// <summary>攻撃を始めてからポータルを開くまでの遅れ（秒）。</summary>
        public readonly float Delay;

        /// <summary>突き出した後にプレイヤーの方へ薙ぎ払うか。</summary>
        public readonly bool Sweep;

        public KurageTentacleSpawn(Vector3 position, float delay, bool sweep)
        {
            Position = position;
            Delay = delay;
            Sweep = sweep;
        }
    }

    /// <summary>
    /// 地面から触手を出す攻撃の「どこに・いつ・薙ぎ払うか」を決める。
    /// 出し方は3パターン作る予定で、まだ決まっていないため、パターンごとにアセットを作って差し替えられるようにしてある。
    /// </summary>
    public abstract class KurageTentaclePattern : ScriptableObject
    {
        /// <param name="playerPosition">攻撃を始めた時のプレイヤーの位置</param>
        /// <param name="stageTop">ステージ上面の範囲。出現位置はこの中に収める</param>
        public abstract IReadOnlyList<KurageTentacleSpawn> Build(
            Vector3 playerPosition,
            Bounds stageTop
        );

        /// <summary>出現位置をステージ上面の内側(縁から inset だけ内)へ収め、高さを上面に合わせる。</summary>
        protected static Vector3 ClampToStageTop(Vector3 position, Bounds stageTop, float inset)
        {
            position.x = Mathf.Clamp(position.x, stageTop.min.x + inset, stageTop.max.x - inset);
            position.z = Mathf.Clamp(position.z, stageTop.min.z + inset, stageTop.max.z - inset);
            position.y = stageTop.max.y;
            return position;
        }
    }
}
