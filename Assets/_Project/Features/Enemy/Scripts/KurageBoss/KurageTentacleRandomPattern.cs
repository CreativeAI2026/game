using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// ステージ上のランダムな位置に複数本を同時に出し、それを決まった間隔で何回か繰り返すパターン。
    /// 1本ごとに、決まった確率で突き出した後に薙ぎ払う。
    /// </summary>
    [CreateAssetMenu(
        fileName = "KurageTentacleRandomPattern",
        menuName = "creativeAI/Kurage/Tentacle Pattern (Random)"
    )]
    public class KurageTentacleRandomPattern : KurageTentaclePattern
    {
        // 他の触手から離れた位置が見つからない時に選び直す回数の上限(狭いステージで止まらないように)
        private const int MaxPlacementAttempts = 10;

        [Tooltip("一回で出す本数")]
        [SerializeField, Min(1)]
        private int _spawnCount = 5;

        [Tooltip("何回突き出しを行うかの最低値。")]
        [SerializeField, Min(1)]
        private int _waveMinCount = 3;

        [Tooltip("何回突き出しを行うかの最大値。")]
        [SerializeField, Min(1)]
        private int _waveMaxCount = 5;

        [Tooltip("突き出し後に振る確率")]
        [SerializeField, Range(0f, 1f)]
        private float _sweepChance = 0.3f;

        [Tooltip("突き出し後の次の突き出しが出るまでの時間間隔")]
        [SerializeField]
        private float _interval = 1.0f;

        [Tooltip("ステージの縁から内側へ離す距離（m）。縁ぎりぎりに出さない")]
        [SerializeField]
        private float _edgeInset = 2f;

        [Tooltip("同じ回に出す触手どうしを離す距離（m）。ポータルや触手が重ならないようにする")]
        [SerializeField]
        private float _minSpacing = 4f;

        public override IReadOnlyList<KurageTentacleSpawn> Build(
            Vector3 playerPosition,
            Bounds stageTop
        )
        {
            int waveCount = Random.Range(_waveMinCount, _waveMaxCount + 1);
            var spawns = new List<KurageTentacleSpawn>(_spawnCount * waveCount);

            for (int wave = 0; wave < waveCount; wave++)
            {
                float delay = wave * _interval;
                int waveStart = spawns.Count;

                for (int i = 0; i < _spawnCount; i++)
                {
                    bool sweep = Random.value < _sweepChance;
                    spawns.Add(
                        new KurageTentacleSpawn(
                            PickPosition(stageTop, spawns, waveStart),
                            delay,
                            sweep
                        )
                    );
                }
            }

            return spawns;
        }

        /// <summary>
        /// ステージの縁より内側のランダムな位置を選ぶ。同じ回の触手から離れた位置が見つからなければ、最後に選んだ位置を使う。
        /// </summary>
        private Vector3 PickPosition(
            Bounds stageTop,
            List<KurageTentacleSpawn> spawns,
            int waveStart
        )
        {
            Vector3 position = Vector3.zero;
            for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
            {
                position = new Vector3(
                    Random.Range(stageTop.min.x + _edgeInset, stageTop.max.x - _edgeInset),
                    stageTop.max.y,
                    Random.Range(stageTop.min.z + _edgeInset, stageTop.max.z - _edgeInset)
                );
                if (IsSpacedFrom(position, spawns, waveStart, _minSpacing))
                    break;
            }
            return position;
        }

        private void OnValidate()
        {
            // 最大が最低を下回ると回数が意図しない値になるため揃える
            _waveMaxCount = Mathf.Max(_waveMaxCount, _waveMinCount);
        }
    }
}
