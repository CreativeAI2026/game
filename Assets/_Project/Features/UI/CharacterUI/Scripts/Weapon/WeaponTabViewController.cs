using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI.CharacterUI
{
    /// <summary>
    /// キャラクター画面の武器タブ(剣/弓/鎌)。選択中のタブに応じて、その武器の
    /// <b>固定ステータス</b>を表示する。閲覧専用で装備・切替はしない
    /// (実際の切替はフィールドの武器切替UI)。武器の固定値は表示用にここへ保持する。
    /// </summary>
    public class WeaponTabViewController : MonoBehaviour
    {
        [SerializeField]
        private RevolverTabGroup _revolverTabGroup;

        [SerializeField]
        private TMP_Text _weaponName;

        [SerializeField]
        private TMP_Text _weaponStats;

        [Header("Detail Transition")]
        [SerializeField, Min(0f)]
        private float _fadeOutDuration = 0.1f;

        [SerializeField, Min(0f)]
        private float _fadeInDuration = 0.16f;

        private Sequence _detailTransition;
        private bool _hasDisplayedWeapon;

        // 剣/弓/鎌 の2つずつの固定補正。タブ順(剣→弓→鎌)に合わせる。
        private static readonly (string name, string stats)[] Weapons =
        {
            ("剣", "攻撃%              +25%\n\n会心ダメージ      +50%"),
            ("弓", "攻撃%              +25%\n\n会心率            +50%"),
            ("鎌", "会心率            +50%\n\n会心ダメージ      +50%"),
        };

        private void OnEnable()
        {
            if (_revolverTabGroup != null)
            {
                _revolverTabGroup.SelectionChanged += OnTabSelected;
                if (_revolverTabGroup.CurrentIndex >= 0)
                    Show(_revolverTabGroup.CurrentIndex);
            }
            else
            {
                Show(0);
            }
        }

        private void OnDisable()
        {
            if (_revolverTabGroup != null)
                _revolverTabGroup.SelectionChanged -= OnTabSelected;

            KillDetailTransition();
            RestoreDetailVisuals();
            _hasDisplayedWeapon = false;
        }

        private void OnTabSelected(int index, TabDefinition definition, GameObject view) =>
            Show(index);

        private void Show(int index)
        {
            if (index < 0 || index >= Weapons.Length)
                return;

            if (!_hasDisplayedWeapon || !isActiveAndEnabled)
            {
                ApplyText(index);
                RestoreDetailVisuals();
                _hasDisplayedWeapon = true;
                return;
            }

            AnimateTextChange(index);
        }

        private void AnimateTextChange(int index)
        {
            KillDetailTransition();

            _detailTransition = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            if (_weaponName != null)
                _detailTransition.Join(_weaponName.DOFade(0f, _fadeOutDuration));

            if (_weaponStats != null)
                _detailTransition.Join(_weaponStats.DOFade(0f, _fadeOutDuration));

            _detailTransition.AppendCallback(() => ApplyText(index));

            if (_weaponName != null)
                _detailTransition.Append(_weaponName.DOFade(1f, _fadeInDuration));

            if (_weaponStats != null)
                _detailTransition.Join(_weaponStats.DOFade(1f, _fadeInDuration));

            _detailTransition.OnComplete(() => _detailTransition = null);
        }

        private void ApplyText(int index)
        {
            if (_weaponName != null)
                _weaponName.text = Weapons[index].name;
            if (_weaponStats != null)
                _weaponStats.text = Weapons[index].stats;
        }

        private void RestoreDetailVisuals()
        {
            RestoreTextVisual(_weaponName);
            RestoreTextVisual(_weaponStats);
        }

        private static void RestoreTextVisual(TMP_Text text)
        {
            if (text == null)
                return;

            text.DOKill();
            text.rectTransform.DOKill();
            text.alpha = 1f;
        }

        private void KillDetailTransition()
        {
            if (_detailTransition == null)
                return;

            _detailTransition.Kill();
            _detailTransition = null;
        }
    }
}
