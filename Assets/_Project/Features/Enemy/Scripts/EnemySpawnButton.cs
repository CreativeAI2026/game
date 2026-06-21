using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// テスト用：3Dオブジェクトのボタン。弓矢が当たると敵をスポーンする。
    ///
    /// ■ 使い方
    ///   1. このスクリプトを任意の3DオブジェクトにアタッチしてColliderを付ける。
    ///   2. Inspectorで以下を設定する。
    ///      - Enemy Prefab     : スポーンする敵のPrefab
    ///      - Spawn Points     : スポーン地点のTransformを1〜複数指定（空の場合はボタン自身の位置）
    ///      - Max Enemy Count  : フィールド上の最大同時存在数（デフォルト3）
    ///      - Enemy Tag        : スポーン済み敵を数えるためのタグ（デフォルト "Enemy"）
    /// </summary>
    public class EnemySpawnButton : MonoBehaviour, IArrowHittable
    {
        [Header("スポーン設定")]
        [Tooltip("スポーンする敵のPrefab")]
        [SerializeField]
        private GameObject _enemyPrefab;

        [Tooltip(
            "スポーン地点のTransform（複数指定可能）。\n"
                + "空の場合はボタン自身の位置にスポーンする。\n"
                + "スポーン時にリストの先頭から順番に使用する。"
        )]
        [SerializeField]
        private List<Transform> _spawnPoints = new List<Transform>();

        [Tooltip("フィールド上に同時存在できる敵の最大数")]
        [SerializeField]
        private int _maxEnemyCount = 3;

        [Tooltip("敵を数えるためのタグ名")]
        [SerializeField]
        private string _enemyTag = "Enemy";

        // 次にスポーンするスポーンポイントのインデックス
        private int _nextSpawnIndex = 0;

        // -------------------------------------------------------
        // IArrowHittable 実装
        // -------------------------------------------------------

        /// <summary>
        /// ArrowTip から呼ばれる。敵の数を確認してスポーンを試みる。
        /// </summary>
        public void OnArrowHit()
        {
            TrySpawnEnemy();
        }

        // -------------------------------------------------------
        // スポーン処理
        // -------------------------------------------------------

        private void TrySpawnEnemy()
        {
            if (_enemyPrefab == null)
            {
                Debug.LogWarning("[EnemySpawnButton] EnemyPrefab が未設定です。");
                return;
            }

            // フィールド上の敵の数を確認
            int currentEnemyCount = GameObject.FindGameObjectsWithTag(_enemyTag).Length;

            if (currentEnemyCount >= _maxEnemyCount)
            {
                Debug.Log(
                    $"[EnemySpawnButton] 敵が最大数（{_maxEnemyCount}体）に達しているためスポーンしません。"
                );
                return;
            }

            // スポーン位置を決定
            Vector3 spawnPosition;
            Quaternion spawnRotation;

            if (_spawnPoints != null && _spawnPoints.Count > 0)
            {
                // 有効なスポーンポイントを探す（nullスキップ）
                Transform spawnPoint = GetNextValidSpawnPoint();
                if (spawnPoint != null)
                {
                    spawnPosition = spawnPoint.position;
                    spawnRotation = spawnPoint.rotation;
                }
                else
                {
                    // 全スポーンポイントがnullなら自身の位置にフォールバック
                    spawnPosition = transform.position;
                    spawnRotation = transform.rotation;
                }
            }
            else
            {
                // スポーンポイント未設定の場合はボタン自身の位置
                spawnPosition = transform.position;
                spawnRotation = transform.rotation;
            }

            GameObject spawned = Instantiate(_enemyPrefab, spawnPosition, spawnRotation);
            Debug.Log(
                $"[EnemySpawnButton] 敵をスポーンしました。現在の敵数: {currentEnemyCount + 1}/{_maxEnemyCount}"
            );
        }

        /// <summary>
        /// スポーンポイントリストを順番に参照し、次の有効なTransformを返す。
        /// </summary>
        private Transform GetNextValidSpawnPoint()
        {
            if (_spawnPoints == null || _spawnPoints.Count == 0)
                return null;

            int startIndex = _nextSpawnIndex;
            // リストをひと通り試す
            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                int index = (startIndex + i) % _spawnPoints.Count;
                if (_spawnPoints[index] != null)
                {
                    // 次回は次のインデックスから
                    _nextSpawnIndex = (index + 1) % _spawnPoints.Count;
                    return _spawnPoints[index];
                }
            }

            return null;
        }

        // -------------------------------------------------------
        // Gizmos（エディタ上でスポーンポイントを可視化）
        // -------------------------------------------------------

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // ボタン自身を黄色で表示
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.3f);

            if (_spawnPoints == null)
                return;

            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                if (_spawnPoints[i] == null)
                    continue;

                // スポーンポイントを緑で表示
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_spawnPoints[i].position, 0.4f);

                // ボタンからスポーンポイントへ線を引く
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, _spawnPoints[i].position);

                // ラベルを表示
                UnityEditor.Handles.Label(
                    _spawnPoints[i].position + Vector3.up * 0.5f,
                    $"Spawn {i}"
                );
            }
        }
#endif
    }
}
