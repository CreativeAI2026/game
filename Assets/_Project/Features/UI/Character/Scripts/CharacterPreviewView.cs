using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    public sealed class CharacterPreviewView : MonoBehaviour
    {
        [SerializeField]
        private ModelPreview _characterPreview;

        [SerializeField]
        private AnimationClip _unarmedIdle;

        [SerializeField]
        private AnimationClip _weaponIdle;

        [SerializeField]
        private AnimationClip _meleeAttack;

        [SerializeField]
        private AnimationClip _bowDraw;

        [SerializeField]
        private AnimationClip _bowFire;

        [SerializeField]
        private ModelPreview _weaponPreview;

        [SerializeField]
        private CharacterWeaponSelector _selector;

        [SerializeField]
        private GameObject _weaponPanel;

        [SerializeField]
        private GameObject _weaponChangeBar;
        private WeaponManager _weapons; // 持っている武器と選択(セッション常駐)
        private PlayerWeapons _playerWeapons; // 武器のモデル(プレイヤー)
        private PlayerController _player;
        private float _nextRefresh;
        private bool _dirty;
        private CharacterPreviewPose _posedModel;
        public bool PreviewIsAttacking => _posedModel != null && _posedModel.IsAttacking;
        public bool PreviewIsAnimating => _posedModel != null && _posedModel.IsAnimating;

        /// <summary>プレビューでいま流している攻撃クリップ。攻撃していなければ null。</summary>
        public AnimationClip PreviewAttackClip => _posedModel?.AttackClip;
        public double PreviewAnimationTime => _posedModel != null ? _posedModel.AnimationTime : 0;
        public int PreviewWeaponId => _posedModel != null ? _posedModel.WeaponId : -1;

        private void OnEnable()
        {
            if (_characterPreview != null)
                _characterPreview.Clicked += PlayPreviewAttack;
            PlayerRegistry.Changed += Bind;
            if (_selector != null)
                _selector.SelectionChanged += SelectionChanged;
            Bind(PlayerRegistry.Current);
        }

        private void PlayPreviewAttack()
        {
            // Ignore clicks while selection/equipment is waiting to be synchronized.
            if (_dirty || _posedModel == null)
                return;
            // 素手は待機するだけで、攻撃はしない。
            if (_posedModel.WeaponId < 0)
                return;
            if (_posedModel.WeaponId == CharacterPreviewPose.BowWeaponId)
                _posedModel.PlayAttack(_bowDraw, _bowFire);
            else
                _posedModel.PlayAttack(_meleeAttack);
        }

        private void Bind(PlayerController player)
        {
            Unsubscribe();
            _posedModel?.Dispose();
            _posedModel = null;
            if (_characterPreview != null)
                _characterPreview.SetLiveTarget(null, null, default);
            _player = player;
            _playerWeapons = player != null ? player.GetComponent<PlayerWeapons>() : null;
            _weapons = _playerWeapons != null ? WeaponManager.Instance : null;
            if (_weapons != null)
            {
                _weapons.OwnedChanged += OwnedChanged;
                _weapons.SelectionChanged += EquippedChanged;
            }
            RefreshOwnership();
        }

        private void OwnedChanged() => RefreshOwnership();

        private void EquippedChanged(bool previous) => _dirty = true;

        private void RefreshOwnership()
        {
            bool owned = _weapons != null && _weapons.OwnedCount > 0;
            if (_weaponPanel != null)
                _weaponPanel.SetActive(owned);
            if (_weaponChangeBar != null)
                _weaponChangeBar.SetActive(owned);
            if (_selector != null && _weapons != null)
                _selector.SetOwnedWeapons(_weapons);
            SelectionChanged(_selector != null ? _selector.SelectedWeapon : null);
            _dirty = true;
        }

        private void SelectionChanged(WeaponData data)
        {
            _dirty = true;
            if (_weaponPreview == null || !_weaponPreview.isActiveAndEnabled)
                return;
            GameObject source = null;
            if (_playerWeapons != null && data != null)
                source = WeaponObjectOf(data.id);
            _weaponPreview.SetSources(
                source != null ? source.transform : null,
                source != null
                    ? VisibleRenderers(source.transform, true)
                    : System.Array.Empty<Renderer>()
            );
        }

        // 武器 ID のモデル(プレイヤーに配線した武器のルート)。無ければ null。
        private GameObject WeaponObjectOf(int weaponId)
        {
            var catalog = WeaponCatalog.Instance;
            return _playerWeapons != null && catalog != null
                ? _playerWeapons.GetWeaponObject(catalog.IndexOf(weaponId))
                : null;
        }

        private void LateUpdate()
        {
            if (_weapons == null || _player == null)
                return;
            if (_characterPreview == null)
                return;
            // モデルが差し替わったら準備し直す。
            if (_posedModel != null && _posedModel.Source == null)
                _dirty = true;
            if (_dirty)
                Prepare();
            else if (_posedModel != null && Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + .25f;
                _posedModel.SynchronizeAppearance();
            }
            // 動かすだけ。モデルは作り直さず、ModelPreview がそのまま撮る。
            _posedModel?.Tick(Time.unscaledDeltaTime);
        }

        // モデル・武器が変わったときだけ呼ぶ。コピーの用意とカメラ距離の決め直しをする。
        private void Prepare()
        {
            _dirty = false;
            _nextRefresh = Time.unscaledTime + .25f;
            var model = _player.GetComponentInChildren<PlayerModel>(true);
            if (model == null)
                return;
            if (_posedModel == null || _posedModel.Source != model)
            {
                _posedModel?.Dispose();
                _posedModel = new CharacterPreviewPose(
                    model,
                    _playerWeapons,
                    _unarmedIdle,
                    _weaponIdle
                );
            }
            int weaponId =
                _weapons.OwnedCount > 0 && _weapons.CurrentWeaponIndex >= 0
                    ? (
                        _selector != null && _selector.SelectedWeapon != null
                            ? _selector.SelectedWeapon.id
                            : 1001 + _weapons.CurrentWeaponIndex
                    )
                    : -1;
            if (_posedModel.WeaponId != weaponId)
                _posedModel.SetWeapon(weaponId >= 0 ? WeaponObjectOf(weaponId) : null, weaponId);
            _posedModel.SynchronizeAppearance();
            _characterPreview.SetLiveTarget(
                _posedModel.Root,
                _posedModel.Renderers,
                _posedModel.Framing
            );
        }

        private static IEnumerable<Renderer> VisibleRenderers(
            Transform root,
            bool includeInactiveRoot
        )
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                bool visible = true;
                for (var current = renderer.transform; current != null; current = current.parent)
                {
                    if (current == root)
                    {
                        if (!includeInactiveRoot && !current.gameObject.activeSelf)
                            visible = false;
                        break;
                    }
                    if (!current.gameObject.activeSelf)
                    {
                        visible = false;
                        break;
                    }
                }
                if (visible)
                    yield return renderer;
            }
        }

        private void Unsubscribe()
        {
            if (_weapons == null)
                return;
            _weapons.OwnedChanged -= OwnedChanged;
            _weapons.SelectionChanged -= EquippedChanged;
        }

        private void OnDisable()
        {
            if (_characterPreview != null)
                _characterPreview.Clicked -= PlayPreviewAttack;
            PlayerRegistry.Changed -= Bind;
            if (_selector != null)
                _selector.SelectionChanged -= SelectionChanged;
            Unsubscribe();
            if (_characterPreview != null)
                _characterPreview.SetLiveTarget(null, null, default);
            _posedModel?.Dispose();
            _posedModel = null;
            _weapons = null;
            _playerWeapons = null;
        }
    }
}
