using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 装備によるバフ(効果)と、HUD に出すアイコンの組。
    /// 並び順がそのまま HUD の左からの並び順になる。状態異常(デバフ)のアイコンはバフの後ろに並ぶ。
    /// </summary>
    [CreateAssetMenu(
        fileName = "EquipmentBuffIconSet",
        menuName = "Scriptable Objects/UI/EquipmentBuffIconSet"
    )]
    public sealed class EquipmentBuffIconSet : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("バフの効果(装備の補正の種類)")]
            public StatType effect;

            [Tooltip("この効果が付いているときに出すアイコン")]
            public Sprite icon;
        }

        [SerializeField]
        private List<Entry> _entries = new();

        [Header("状態異常(デバフ)")]
        [Tooltip("毒(継続ダメージ)にかかっている間に出すアイコン")]
        [SerializeField]
        private Sprite _poisonIcon;

        public IReadOnlyList<Entry> Entries => _entries;

        public Sprite PoisonIcon => _poisonIcon;
    }
}
