using DG.Tweening;
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

        [SerializeField, Min(0f)]
        private float _slideDistance = 18f;

        private Sequence _detailTransition;
        private Vector2 _weaponNameBasePosition;
        private Vector2 _weaponStatsBasePosition;
        private bool _hasDisplayedWeapon;

        // spec §1.1: 剣/弓/鎌 の2つずつの固定補正。タブ順(剣→弓→鎌)に合わせる。
        private static readonly (string name, string stats)[] Weapons =
        {
            ("剣", "攻撃%              +25%\n\n会心ダメージ      +50%"),
            ("弓", "攻撃%              +25%\n\n会心率            +50%"),
            ("鎌", "会心率            +50%\n\n会心ダメージ      +50%"),
        };

        private void Awake()
        {
            if (_weaponName != null)
                _weaponNameBasePosition = _weaponName.rectTransform.anchoredPosition;
            if (_weaponStats != null)
                _weaponStatsBasePosition = _weaponStats.rectTransform.anchoredPosition;
        }

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
            {
                _detailTransition.Join(_weaponName.DOFade(0f, _fadeOutDuration));
                _detailTransition.Join(
                    _weaponName.rectTransform.DOAnchorPosX(
                        _weaponNameBasePosition.x - _slideDistance,
                        _fadeOutDuration
                    )
                );
            }

            if (_weaponStats != null)
            {
                _detailTransition.Join(_weaponStats.DOFade(0f, _fadeOutDuration));
                _detailTransition.Join(
                    _weaponStats.rectTransform.DOAnchorPosX(
                        _weaponStatsBasePosition.x - _slideDistance,
                        _fadeOutDuration
                    )
                );
            }

            _detailTransition.AppendCallback(() =>
            {
                ApplyText(index);
                PrepareDetailEntrance();
            });

            if (_weaponName != null)
            {
                _detailTransition.Append(
                    _weaponName.rectTransform.DOAnchorPosX(
                        _weaponNameBasePosition.x,
                        _fadeInDuration
                    )
                );
                _detailTransition.Join(_weaponName.DOFade(1f, _fadeInDuration));
            }

            if (_weaponStats != null)
            {
                _detailTransition.Join(
                    _weaponStats.rectTransform.DOAnchorPosX(
                        _weaponStatsBasePosition.x,
                        _fadeInDuration
                    )
                );
                _detailTransition.Join(_weaponStats.DOFade(1f, _fadeInDuration));
            }

            _detailTransition.OnComplete(() => _detailTransition = null);
        }

        private void ApplyText(int index)
        {
            if (_weaponName != null)
                _weaponName.text = Weapons[index].name;
            if (_weaponStats != null)
                _weaponStats.text = Weapons[index].stats;
        }

        private void PrepareDetailEntrance()
        {
            if (_weaponName != null)
                _weaponName.rectTransform.anchoredPosition =
                    _weaponNameBasePosition + Vector2.right * _slideDistance;
            if (_weaponStats != null)
                _weaponStats.rectTransform.anchoredPosition =
                    _weaponStatsBasePosition + Vector2.right * _slideDistance;
        }

        private void RestoreDetailVisuals()
        {
            RestoreTextVisual(_weaponName, _weaponNameBasePosition);
            RestoreTextVisual(_weaponStats, _weaponStatsBasePosition);
        }

        private static void RestoreTextVisual(TMP_Text text, Vector2 position)
        {
            if (text == null)
                return;

            text.DOKill();
            text.rectTransform.DOKill();
            text.alpha = 1f;
            text.rectTransform.anchoredPosition = position;
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
