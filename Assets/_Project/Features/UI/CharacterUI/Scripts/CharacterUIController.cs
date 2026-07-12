using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.UI.CharacterUI
{
    public partial class CharacterUIController : MonoBehaviour
    {
        [Header("Tabs"), SerializeField]
        private TabGroup _tabGroup;

        private readonly List<EquipmentViewController> _equipmentViewControllers = new();
        private bool _initialized;
        private bool _resetOnNextEnable;

        private void Start()
        {
            CollectEquipmentViews();
            foreach (var controller in _equipmentViewControllers)
                controller?.EnsureInitialized();

            if (_tabGroup != null)
                _tabGroup.OnTabSelected += OnTabSelected;

            _initialized = true;
        }

        private void OnEnable()
        {
            if (_initialized && _resetOnNextEnable)
                StartCoroutine(ResetAfterOpen());
        }

        private void OnDisable()
        {
            if (_initialized)
                _resetOnNextEnable = true;
        }

        private void OnDestroy()
        {
            if (_tabGroup != null)
                _tabGroup.OnTabSelected -= OnTabSelected;
        }

        private IEnumerator ResetAfterOpen()
        {
            yield return null;

            _resetOnNextEnable = false;
            _tabGroup?.ResetToFirstTab();

            foreach (var controller in _equipmentViewControllers)
                controller?.ResetViewState();
        }

        private void OnTabSelected(int index)
        {
            foreach (var controller in _equipmentViewControllers)
            {
                if (controller == null)
                    continue;

                if (!controller.gameObject.activeInHierarchy)
                    controller.OnExit();
            }

            foreach (var controller in _equipmentViewControllers)
            {
                if (controller != null && controller.gameObject.activeInHierarchy)
                    controller.OnEnter();
            }
        }

        private void CollectEquipmentViews()
        {
            _equipmentViewControllers.Clear();
            if (_tabGroup == null)
                return;

            for (int i = 0; i < _tabGroup.EntryCount; i++)
            {
                var view = _tabGroup.GetView(i);
                if (view == null)
                    continue;

                foreach (
                    var controller in view.GetComponentsInChildren<EquipmentViewController>(true)
                )
                {
                    if (controller != null && !_equipmentViewControllers.Contains(controller))
                        _equipmentViewControllers.Add(controller);
                }
            }
        }
    }
}
