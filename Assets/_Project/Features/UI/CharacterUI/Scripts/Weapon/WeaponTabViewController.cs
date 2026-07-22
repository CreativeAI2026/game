using TMPro;
using UnityEngine;

namespace CreativeAI.UI.CharacterUI
{
    /// <summary>
    /// キャラクター画面の武器タブ(剣/弓/鎌)。選択中のタブに応じて、その武器の
    /// <b>固定ステータス</b>(spec §1.1)を表示する。閲覧専用で装備・切替はしない
    /// (実際の切替はフィールドの武器切替UI)。武器の固定値は spec 定義のため表示用にここへ保持する。
    /// </summary>
    public class WeaponTabViewController : MonoBehaviour
    {
        [SerializeField]
        private TabGroup _tabGroup;

        [SerializeField]
        private RevolverTabGroup _revolverTabGroup;

        [SerializeField]
        private TMP_Text _weaponName;

        [SerializeField]
        private TMP_Text _weaponStats;

        // spec §1.1: 剣/弓/鎌 の2つずつの固定補正。タブ順(剣→弓→鎌)に合わせる。
        private static readonly (string name, string stats)[] Weapons =
        {
            ("剣", "攻撃%              +25%\n\n会心ダメージ      +50%"),
            ("弓", "攻撃%              +25%\n\n会心率            +50%"),
            ("鎌", "会心率            +50%\n\n会心ダメージ      +50%"),
        };

        private void OnEnable()
        {
            if (_revolverTabGroup != null)
                _revolverTabGroup.SelectionChanged += OnTabSelected;
            else if (_tabGroup != null)
                _tabGroup.OnSelectionChanged += OnTabSelected;
            Show(GetCurrentIndex());
        }

        private void OnDisable()
        {
            if (_revolverTabGroup != null)
                _revolverTabGroup.SelectionChanged -= OnTabSelected;
            else if (_tabGroup != null)
                _tabGroup.OnSelectionChanged -= OnTabSelected;
        }

        private void OnTabSelected(int index, TabDefinition definition, GameObject view) =>
            Show(index);

        private void Show(int index)
        {
            if (index < 0 || index >= Weapons.Length)
                return;
            if (_weaponName != null)
                _weaponName.text = Weapons[index].name;
            if (_weaponStats != null)
                _weaponStats.text = Weapons[index].stats;
        }

        private int GetCurrentIndex()
        {
            if (_revolverTabGroup != null)
                return Mathf.Max(0, _revolverTabGroup.CurrentIndex);
            return _tabGroup != null ? Mathf.Max(0, _tabGroup.CurrentIndex) : 0;
        }
    }
}
