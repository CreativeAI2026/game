using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 仮のパターン: プレイヤーの足元に1本、その周りに輪状に数本を少しずつずらして出し、最後の1本だけ薙ぎ払う。
    /// 正式な3パターンが決まったら、同じように <see cref="KurageTentaclePattern"/> を継承して作る。
    /// </summary>
    [CreateAssetMenu(
        fileName = "KurageTentacleAroundPlayerPattern",
        menuName = "creativeAI/Kurage/Tentacle Pattern (Around Player)"
    )]
    public class KurageTentacleAroundPlayerPattern : KurageTentaclePattern
    {
        [Tooltip("周りに出す本数(足元の1本は別)")]
        [SerializeField]
        private int _ringCount = 3;

        [Tooltip("周りに出す触手とプレイヤーの距離（m）")]
        [SerializeField]
        private float _ringRadius = 6f;

        [Tooltip("1本ごとにずらす時間（秒）")]
        [SerializeField]
        private float _interval = 0.35f;

        [Tooltip("ステージの縁から内側へ離す距離（m）。縁ぎりぎりに出さない")]
        [SerializeField]
        private float _edgeInset = 2f;

        public override IReadOnlyList<KurageTentacleSpawn> Build(
            Vector3 playerPosition,
            Bounds stageTop
        )
        {
            var spawns = new List<KurageTentacleSpawn>
            {
                new(ClampToStageTop(playerPosition, stageTop, _edgeInset), 0f, false),
            };

            float startAngle = Random.Range(0f, 360f);
            for (int i = 0; i < _ringCount; i++)
            {
                float angle = (startAngle + 360f * i / _ringCount) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _ringRadius;
                bool isLast = i == _ringCount - 1;
                spawns.Add(
                    new(
                        ClampToStageTop(playerPosition + offset, stageTop, _edgeInset),
                        _interval * (i + 1),
                        isLast
                    )
                );
            }
            return spawns;
        }
    }
}
