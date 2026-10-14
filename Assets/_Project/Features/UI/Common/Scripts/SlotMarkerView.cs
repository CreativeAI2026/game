using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.UI
{
    public class SlotMarkerView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _equippedMarker;

        [SerializeField]
        private GameObject _craftAssignedMarker;

        [SerializeField]
        private GameObject _equippedDimOverlay;

        private bool _isEquipped;
        private bool _isCraftAssigned;
        private readonly HashSet<string> _warnedMissingMarkers = new();

        public void SetEquipped(bool equipped)
        {
            _isEquipped = equipped;
            ResolveReferences();
            ApplyStates();
        }

        public void SetCraftAssigned(bool assigned)
        {
            _isCraftAssigned = assigned;
            ResolveReferences();
            ApplyStates();
        }

        private void ResolveReferences()
        {
            if (_equippedMarker == null)
                WarnMissingMarkerOnce("EquippedMarker");
            if (_craftAssignedMarker == null)
                WarnMissingMarkerOnce("CraftAssignedMarker");
            if (_equippedDimOverlay == null)
                WarnMissingMarkerOnce("EquippedDimOverlay");
        }

        private void ApplyStates()
        {
            _equippedMarker?.SetActive(_isEquipped);
            _equippedDimOverlay?.SetActive(_isEquipped);
            _craftAssignedMarker?.SetActive(_isCraftAssigned && !_isEquipped);
        }

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _equippedMarker ??= FindExisting("VisualRoot/EquippedMarker", "EquippedMarker");
            _craftAssignedMarker ??= FindExisting(
                "VisualRoot/CraftAssignedMarker",
                "CraftAssignedMarker"
            );
            _equippedDimOverlay ??= FindExisting(
                "VisualRoot/EquippedDimOverlay",
                "EquippedDimOverlay"
            );
        }

        private GameObject FindExisting(string primaryPath, string fallbackPath)
        {
            var target = transform.Find(primaryPath) ?? transform.Find(fallbackPath);
            return target != null ? target.gameObject : null;
        }
#endif

        private void WarnMissingMarkerOnce(string markerName)
        {
            if (!_warnedMissingMarkers.Add(markerName))
                return;

            Debug.LogWarning(
                $"{nameof(SlotMarkerView)} '{name}' に {markerName} がないため、そのMarker表示をスキップします。Prefab上で設定してください。",
                this
            );
        }
    }
}
