using UnityEngine;

namespace CreativeAI.Gameplay
{
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Scriptable Objects/WeaponData")]
    public class WeaponData : ItemData
    {
        public int attack; // 攻撃
        public int defense; // 防御
        public float criticalDamage; // 会心ダメージ
        public float criticalRate; // 会心率
        public int maxHP; // 最大HP
        // 武器はインベントリの3カテゴリに属さない(WeaponManager 管理)。category は使用しない。
        // ItemData を継承するのはアイコン/名前/ステータス表示の基盤を再利用するためだけ。
    }
}
