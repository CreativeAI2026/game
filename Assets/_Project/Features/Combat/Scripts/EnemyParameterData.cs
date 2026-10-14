using UnityEngine;

namespace CreativeAI.Gameplay
{
    [CreateAssetMenu(
        fileName = "NewEnemyParameterData",
        menuName = "creativeAI/EnemyParameterData"
    )]
    public class EnemyParameterData : ScriptableObject
    {
        [Header("敵の情報")]
        public string characterName; // キャラクター名

        [Min(1f)]
        public float baseMaxLife = 5000f; // 最大体力の基準値

        [Min(0f)]
        public float baseAttackPower = 100f; // 攻撃力の基準値

        [Min(0f)]
        public float baseDefense = 10f; //防御力の基準値
        public float baseMoveSpeed = 5f; // 移動速度の基準値

        [Header("怯み（スーパーアーマー）設定")]
        [Tooltip("一度怯んだ後、再度怯むようになるまでの無敵時間（秒）")]
        [Min(0f)]
        public float flinchCooldownTime = 10f;

        [Tooltip("単発でこの値以上のダメージを受けたら強制的に怯む")]
        [Min(1f)]
        public float flinchDamageThreshold = 150f;
    }
}
