using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 装備によるバフ(効果)と、HUD に出すアイコンの組。
    /// 並び順がそのまま HUD の左からの並び順になる。
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentBuffIconSet", menuName = "Scriptable Objects/UI/EquipmentBuffIconSet")]
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

        public IReadOnlyList<Entry> Entries => _entries;
    }
}
