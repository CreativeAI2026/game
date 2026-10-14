using UnityEngine;

namespace CreativeAI.Gameplay
{
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Scriptable Objects/WeaponData")]
    public class WeaponData : ScriptableObject
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
    }
}
