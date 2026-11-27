using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>武器1本の定義(名前・アイコン・補正の値)。一覧は <see cref="WeaponCatalog"/>。</summary>
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Scriptable Objects/WeaponData")]
    public sealed class WeaponData : ScriptableObject
    {
        [Header("基本情報")]
        public Sprite icon;

        public int id;
        public string itemName;

        [TextArea]
        public string description;

        [Header("武器ステータス")]
        [Tooltip("攻撃力補正（%）")]
        public int attack; // 攻撃
        public float criticalDamage; // 会心ダメージ
        public float criticalRate; // 会心率

        /// <summary>
        /// 選んでいるときに最終ステータスへ乗る補正(装備品と同じ形)。移動速度/攻撃速度は PlayerStatus の対象外なので含めない。
        /// </summary>
        public EquipmentBonus Bonus =>
            new EquipmentBonus
            {
                attackPct = attack,
                criticalChance = criticalRate,
                criticalDamage = criticalDamage,
            };
    }
}
