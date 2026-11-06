using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの周りを囲むように、円形に等間隔で複数本を同時に出し、すべて薙ぎ払うパターン。
    /// 突き出してから薙ぎ払うまでの間に円の外へ逃げれば避けられる。
    /// ステージの縁で位置を内側へ寄せた結果、他の触手と重なる位置になったものは出さない。
    /// </summary>
    [CreateAssetMenu(
        fileName = "KurageTentacleCirclePlayerPattern",
        menuName = "creativeAI/Kurage/Tentacle Pattern (Circle Player)"
    )]
    public class KurageTentacleCirclePlayerPattern : KurageTentaclePattern
    {
        [Tooltip("囲む本数")]
        [SerializeField, Min(1)]
        private int _count = 8;

        [Tooltip("プレイヤーから円までの距離（m）")]
        [SerializeField]
        private float _radius = 5f;

        [Tooltip("ステージの縁から内側へ離す距離（m）。縁ぎりぎりに出さない")]
        [SerializeField]
        private float _edgeInset = 2f;

        [Tooltip(
            "触手どうしをこれより近くに出さない距離（m）。ステージの縁で寄せられて重なったものを出さないのに使う"
        )]
        [SerializeField]
        private float _minSpacing = 2f;

        public override IReadOnlyList<KurageTentacleSpawn> Build(
            Vector3 playerPosition,
            Bounds stageTop
        )
        {
            var spawns = new List<KurageTentacleSpawn>(_count);
            // 毎回同じ向きに並ばないよう、円の始まりの向きをずらす
            float startAngle = Random.Range(0f, 360f);
            for (int i = 0; i < _count; i++)
            {
                float angle = (startAngle + 360f * i / _count) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _radius;
                Vector3 position = ClampToStageTop(playerPosition + offset, stageTop, _edgeInset);
                if (IsSpacedFrom(position, spawns, 0, _minSpacing))
                    spawns.Add(new KurageTentacleSpawn(position, 0f, true));
            }
            return spawns;
        }
    }
}
