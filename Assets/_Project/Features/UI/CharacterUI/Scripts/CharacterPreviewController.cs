using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI.CharacterUI
{
    public sealed class CharacterPreviewController : MonoBehaviour
    {
        [SerializeField]
        private ModelPreview _characterPreview;

        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("_bladeIdle")]
        private AnimationClip _weaponIdle;

        [SerializeField]
        private AnimationClip _meleeAttack;

        [SerializeField]
        private ModelPreview _weaponPreview;

        [SerializeField]
        private CharacterWeaponSelector _selector;

        [SerializeField]
        private GameObject _weaponPanel;

        [SerializeField]
        private GameObject _weaponChangeBar;
        private WeaponManager _manager;
        private PlayerController _player;
        private float _nextRefresh;
        private bool _dirty;
        private WeaponData _shownWeapon;
        private CharacterPreviewPose _posedModel;
        public float PreviewKneeBend => _posedModel != null ? _posedModel.KneeBend : 0f;
        public bool PreviewIsAttacking => _posedModel != null && _posedModel.IsAttacking;
        public bool PreviewIsAnimating => _posedModel != null && _posedModel.IsAnimating;
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
            if (!_dirty && _posedModel != null)
                _posedModel.PlayAttack(_meleeAttack);
        }

        private void Bind(PlayerController player)
        {
            Unsubscribe();
            _posedModel?.Dispose();
            _posedModel = null;
            _characterPreview?.SetSources(null, System.Array.Empty<Renderer>());
            _player = player;
            _manager = player != null ? player.GetComponent<WeaponManager>() : null;
            if (_manager != null)
            {
                _manager.OnOwnedCountChanged += OwnedChanged;
                _manager.OnWeaponSwitched += EquippedChanged;
            }
            RefreshOwnership();
        }

        private void OwnedChanged(int count) => RefreshOwnership();

        private void EquippedChanged(bool previous) => _dirty = true;

        private void RefreshOwnership()
        {
            bool owned = _manager != null && _manager.OwnedCount > 0;
            _weaponPanel?.SetActive(owned);
            _weaponChangeBar?.SetActive(owned);
            if (_selector != null && _manager != null)
                _selector.SetOwnedWeapons(_manager);
            _shownWeapon = null;
            SelectionChanged(_selector != null ? _selector.SelectedWeapon : null);
            _dirty = true;
        }

        private void SelectionChanged(WeaponData data)
        {
            _dirty = true;
            if (_weaponPreview == null || !_weaponPreview.isActiveAndEnabled)
                return;
            _shownWeapon = data;
            GameObject source = null;
            if (_manager != null && data != null)
                source = _manager.GetWeaponObject(data.id);
            _weaponPreview.SetSources(
                source != null ? source.transform : null,
                source != null
                    ? VisibleRenderers(source.transform, true)
                    : System.Array.Empty<Renderer>()
            );
        }

        private void LateUpdate()
        {
            if (_manager == null || _player == null)
                return;
            if (_selector != null && _shownWeapon != _selector.SelectedWeapon)
                SelectionChanged(_selector.SelectedWeapon);
            if (
                !_dirty
                && Time.unscaledTime < _nextRefresh
                && (_posedModel == null || !_posedModel.IsAnimating)
            )
                return;
            _dirty = false;
            _nextRefresh = Time.unscaledTime + .25f;
            var model = _player.GetComponentInChildren<PlayerModel>(true);
            if (model == null || _characterPreview == null)
                return;
            if (_posedModel == null || _posedModel.Source != model)
            {
                _posedModel?.Dispose();
                _posedModel = new CharacterPreviewPose(model, _manager, _weaponIdle);
            }
            int weaponId =
                _manager.OwnedCount > 0 && _manager.CurrentWeaponIndex >= 0
                    ? (
                        _selector != null && _selector.SelectedWeapon != null
                            ? _selector.SelectedWeapon.id
                            : 1001 + _manager.CurrentWeaponIndex
                    )
                    : -1;
            if (_posedModel.WeaponId != weaponId)
                _posedModel.SetWeapon(
                    weaponId >= 0 ? _manager.GetWeaponObject(weaponId) : null,
                    weaponId
                );
            _posedModel.Tick(Time.unscaledDeltaTime);
            _posedModel.SynchronizeAppearance();
            _characterPreview.SetSources(_posedModel.Root, _posedModel.Renderers);
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
            if (_manager == null)
                return;
            _manager.OnOwnedCountChanged -= OwnedChanged;
            _manager.OnWeaponSwitched -= EquippedChanged;
        }

        private void OnDisable()
        {
            if (_characterPreview != null)
                _characterPreview.Clicked -= PlayPreviewAttack;
            PlayerRegistry.Changed -= Bind;
            if (_selector != null)
                _selector.SelectionChanged -= SelectionChanged;
            Unsubscribe();
            _posedModel?.Dispose();
            _posedModel = null;
            _manager = null;
            _shownWeapon = null;
        }
    }
}
