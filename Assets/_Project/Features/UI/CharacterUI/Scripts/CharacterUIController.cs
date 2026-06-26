using System.Collections;
using UnityEngine;

namespace CreativeAI.UI.CharacterUI
{
    public class CharacterUIController : MonoBehaviour
    {
        [Header("Tabs"), SerializeField]
        private TabGroup _tabGroup;

        [SerializeField]
        private int _equipmentTabIndex = 2;

        private EquipmentViewController _equipmentViewController;
        private bool _initialized;
        private bool _resetOnNextEnable;

        private void Start()
        {
            var equipView = _tabGroup.GetView(_equipmentTabIndex);
            if (equipView != null)
                _equipmentViewController =
                    equipView.GetComponentInChildren<EquipmentViewController>();

            _tabGroup.OnTabSelected += OnTabSelected;
            _initialized = true;
        }

        private void OnDisable()
        {
            if (!_initialized)
                return;

            _resetOnNextEnable = true;
        }

        private void OnEnable()
        {
            if (_initialized && _resetOnNextEnable)
                StartCoroutine(ResetAfterOpen());
        }

        private IEnumerator ResetAfterOpen()
        {
            yield return null;

            _resetOnNextEnable = false;
            _tabGroup?.ResetToFirstTab();
            _equipmentViewController?.ResetViewState();
        }

        private void OnDestroy()
        {
            if (_tabGroup != null)
                _tabGroup.OnTabSelected -= OnTabSelected;
        }

        private void OnTabSelected(int index)
        {
            if (index == _equipmentTabIndex)
                _equipmentViewController?.OnEnter();
            else
                _equipmentViewController?.OnExit();
        }
    }
}
