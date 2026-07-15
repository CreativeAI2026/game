using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// テスト用の敵スポーンボタン。矢が当たると敵をスポーンさせる。
    /// フィールド上の敵数を制限し、過剰なスポーンによる負荷を防ぐ。
    /// </summary>
    public class EnemySpawnButton : MonoBehaviour, IArrowHittable
    {
        [Header("スポーン設定")]
        [Tooltip("スポーンする敵のPrefab"), SerializeField]
        private GameObject _enemyPrefab;

        [Tooltip("スポーン地点のTransform"), SerializeField]
        private List<Transform> _spawnPoints = new();

        [Tooltip("フィールド上に同時存在できる敵の最大数"), SerializeField]
        private int _maxEnemyCount = 3;

        [Tooltip("敵を数えるためのタグ名"), SerializeField]
        private string _enemyTag = "Enemy";

        private int _nextSpawnIndex = 0;

        public void OnArrowHit()
        {
            TrySpawnEnemy();
        }

        private void TrySpawnEnemy()
        {
            if (_enemyPrefab == null)
            {
                Debug.LogWarning("[EnemySpawnButton] EnemyPrefab が未設定です。");
                return;
            }

            int currentEnemyCount = GameObject.FindGameObjectsWithTag(_enemyTag).Length;

            if (currentEnemyCount >= _maxEnemyCount)
            {
                Debug.Log(
                    $"[EnemySpawnButton] 敵が最大数（{_maxEnemyCount}体）に達しているためスポーンしません。"
                );
                return;
            }

            Vector3 spawnPosition;
            Quaternion spawnRotation;

            if (_spawnPoints != null && _spawnPoints.Count > 0)
            {
                Transform spawnPoint = GetNextValidSpawnPoint();
                if (spawnPoint != null)
                {
                    spawnPosition = spawnPoint.position;
                    spawnRotation = spawnPoint.rotation;
                }
                else
                {
                    spawnPosition = transform.position;
                    spawnRotation = transform.rotation;
                }
            }
            else
            {
                spawnPosition = transform.position;
                spawnRotation = transform.rotation;
            }

            GameObject spawned = Instantiate(_enemyPrefab, spawnPosition, spawnRotation);
            Debug.Log(
                $"[EnemySpawnButton] 敵をスポーンしました。現在の敵数: {currentEnemyCount + 1}/{_maxEnemyCount}"
            );
        }

        /// <summary>
        /// ラウンドロビン方式でスポーンポイントを巡回し、同じ地点に連続スポーンするのを避ける。
        /// </summary>
        private Transform GetNextValidSpawnPoint()
        {
            if (_spawnPoints == null || _spawnPoints.Count == 0)
                return null;

            int startIndex = _nextSpawnIndex;
            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                int index = (startIndex + i) % _spawnPoints.Count;
                if (_spawnPoints[index] != null)
                {
                    _nextSpawnIndex = (index + 1) % _spawnPoints.Count;
                    return _spawnPoints[index];
                }
            }

            return null;
        }
    }
}
