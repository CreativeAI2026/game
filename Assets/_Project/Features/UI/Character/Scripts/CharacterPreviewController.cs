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
            if (_characterPreview != null)
                _characterPreview.SetLiveTarget(null, null);
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
            if (_weaponPanel != null)
                _weaponPanel.SetActive(owned);
            if (_weaponChangeBar != null)
                _weaponChangeBar.SetActive(owned);
            if (_selector != null && _manager != null)
                _selector.SetOwnedWeapons(_manager);
            _shownWeapon = null;
            SelectionChanged(_selector != null ? _selector.SelectedWeapon : null);
            _dirty = true;
        }

        private void SelectionChanged(WeaponData data)
        {
            _dirty = true;
            // 武器プレビューが無効でも選択は覚える。覚えないと LateUpdate が毎フレーム「変わった」と見て再準備し続ける。
            _shownWeapon = data;
            if (_weaponPreview == null || !_weaponPreview.isActiveAndEnabled)
                return;
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
            _posedModel.SynchronizeAppearance();
            _characterPreview.SetLiveTarget(_posedModel.Root, _posedModel.Renderers);
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
            if (_characterPreview != null)
                _characterPreview.SetLiveTarget(null, null);
            _posedModel?.Dispose();
            _posedModel = null;
            _manager = null;
            _shownWeapon = null;
        }
    }
}
