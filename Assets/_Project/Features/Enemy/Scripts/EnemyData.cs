using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// enemyKey → Prefab/ステータス の1段目解決(EnemyImplementation.md「2段解決」)。
    /// Id を events.json の enemyKey に一致させる。Features/Enemy/Data に規約配置し、
    /// Importer は Id の集合を「有効な enemyKey カタログ」として使う(専用カタログは新設しない)。
    /// ステータスは既存の EnemyParameterData を流用する(重複定義を避ける)。
    /// </summary>
    [CreateAssetMenu(menuName = "CreativeAI/Enemy Data", fileName = "EnemyData")]
    public sealed class EnemyData : ScriptableObject
    {
        [SerializeField]
        private string _id; // = enemyKey(events.json の battle ステップと対応)

        [SerializeField]
        private GameObject _prefab; // 戦闘に出す敵 Prefab

        [SerializeField]
        private EnemyParameterData _parameters; // ステータス定義(既存 SO を流用)

        public string Id => _id;
        public GameObject Prefab => _prefab;
        public EnemyParameterData Parameters => _parameters;
    }
}
