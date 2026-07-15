using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// enemyKey(文字列)→ 敵 Prefab の対応表。Resources から1回ロードして使い回す読み取り専用データ
    /// (常駐 GameObject ではない)。行に enemyKey と Prefab を並べるだけ(= events.json の battle ステップと対応)。
    /// ステータス・見た目・挙動はすべて Prefab 側(EnemyStatus 等)が持つので、ここはキーと Prefab だけ。
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyDB", menuName = "Scriptable Objects/EnemyDB")]
    public class EnemyDB : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string enemyKey; // = events.json の battle ステップの enemyKey
            public GameObject prefab; // 戦闘に出す敵 Prefab
        }

        private static EnemyDB _instance;
        public static EnemyDB Instance => _instance ??= Resources.Load<EnemyDB>("EnemyDB");

        [SerializeField]
        private List<Entry> enemies = new();

        /// <summary>enemyKey から敵 Prefab を引く。未一致・Prefab 未設定は false。</summary>
        public bool TryGet(string enemyKey, out GameObject prefab)
        {
            prefab = null;
            if (string.IsNullOrEmpty(enemyKey) || enemies == null)
                return false;
            foreach (var e in enemies)
            {
                if (e.enemyKey == enemyKey && e.prefab != null)
                {
                    prefab = e.prefab;
                    return true;
                }
            }
            return false;
        }

        /// <summary>有効な enemyKey の集合(Importer の照合用)。</summary>
        public IEnumerable<string> Keys =>
            enemies == null
                ? Enumerable.Empty<string>()
                : enemies.Where(e => !string.IsNullOrEmpty(e.enemyKey)).Select(e => e.enemyKey);
    }
}
