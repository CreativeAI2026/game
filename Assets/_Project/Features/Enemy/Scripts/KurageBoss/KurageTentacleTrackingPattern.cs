using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーを追いかけるように、決まった間隔で1本ずつ出すパターン。
    /// 出す位置は攻撃を始めた時ではなく、1本ずつ出す瞬間のプレイヤーの足元にする(動き続ければ避けられる)。
    /// </summary>
    [CreateAssetMenu(
        fileName = "KurageTentacleTrackingPattern",
        menuName = "creativeAI/Kurage/Tentacle Pattern (Tracking)"
    )]
    public class KurageTentacleTrackingPattern : KurageTentaclePattern
    {
        [Tooltip("出す本数")]
        [SerializeField, Min(1)]
        private int _count = 6;

        [Tooltip("1本ずつ出す間隔（秒）")]
        [SerializeField]
        private float _interval = 0.6f;

        [Tooltip("突き出した後に薙ぎ払うか")]
        [SerializeField]
        private bool _sweep;

        [Tooltip("ステージの縁から内側へ離す距離（m）。縁ぎりぎりに出さない")]
        [SerializeField]
        private float _edgeInset = 2f;

        public override IReadOnlyList<KurageTentacleSpawn> Build(
            Vector3 playerPosition,
            Bounds stageTop
        )
        {
            // 位置は出す瞬間に決めるので、ここでは時間差だけを決める
            var spawns = new List<KurageTentacleSpawn>(_count);
            for (int i = 0; i < _count; i++)
                spawns.Add(new KurageTentacleSpawn(Vector3.zero, i * _interval, _sweep));
            return spawns;
        }

        public override Vector3 ResolvePosition(
            KurageTentacleSpawn spawn,
            Vector3 playerPosition,
            Bounds stageTop
        ) => ClampToStageTop(playerPosition, stageTop, _edgeInset);
    }
}
