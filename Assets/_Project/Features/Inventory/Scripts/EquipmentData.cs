using UnityEngine;

namespace CreativeAI.Gameplay
{
    [CreateAssetMenu(fileName = "EquipmentData", menuName = "Scriptable Objects/EquipmentData")]
    public class EquipmentData : ItemData
    {
        public int attack; // 攻撃
        public int defense; // 防御
        public float criticalDamage; // 会心ダメージ
        public float criticalRate; // 会心率
        public int maxHP; // 最大HP

        private void OnEnable() => category = ItemCategory.Equipment;

        /// <summary>固定ステータスを能力値の抽選で使うベクトルにする。0 以下の型は落ちる。</summary>
        public static StatVector ToStatVector(EquipmentData e)
        {
            if (e == null)
                return StatVector.Empty;
            return StatVector.Of(
                (StatType.AttackPct, e.attack),
                (StatType.DefensePct, e.defense),
                (StatType.CritDamage, e.criticalDamage),
                (StatType.CritRate, e.criticalRate),
                (StatType.MaxHpPct, e.maxHP)
            );
        }
    }
}
