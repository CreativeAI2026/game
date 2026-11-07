using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>地面の触手1本の出し方。</summary>
    public readonly struct KurageTentacleSpawn
    {
        /// <summary>出現位置(ステージ上面)。出す瞬間に位置を決めるパターンでは使わない(<see cref="KurageTentaclePattern.ResolvePosition"/>)。</summary>
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
    /// パターンごとにアセットを作り、クラゲボスに並べた中から使うたびに1つ選ばれる。
    /// </summary>
    public abstract class KurageTentaclePattern : ScriptableObject
    {
        /// <param name="playerPosition">攻撃を始めた時のプレイヤーの位置</param>
        /// <param name="stageTop">ステージ上面の範囲。出現位置はこの中に収める</param>
        public abstract IReadOnlyList<KurageTentacleSpawn> Build(
            Vector3 playerPosition,
            Bounds stageTop
        );

        /// <summary>
        /// 触手を出す瞬間に、出現位置を決める。既定では <see cref="Build"/> で決めた位置をそのまま使う。
        /// プレイヤーを追いかけるなど、出す瞬間のプレイヤーの位置で決めたいパターンは上書きする。
        /// </summary>
        public virtual Vector3 ResolvePosition(
            KurageTentacleSpawn spawn,
            Vector3 playerPosition,
            Bounds stageTop
        ) => spawn.Position;

        /// <summary>
        /// position が、spawns の startIndex 番目以降(同じ回に出す触手)のどれとも minSpacing 以上離れているか。
        /// ポータルや触手どうしが重ならないようにするのに使う。
        /// </summary>
        protected static bool IsSpacedFrom(
            Vector3 position,
            List<KurageTentacleSpawn> spawns,
            int startIndex,
            float minSpacing
        )
        {
            float minSqr = minSpacing * minSpacing;
            for (int i = startIndex; i < spawns.Count; i++)
            {
                Vector3 offset = spawns[i].Position - position;
                offset.y = 0f;
                if (offset.sqrMagnitude < minSqr)
                    return false;
            }
            return true;
        }

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
