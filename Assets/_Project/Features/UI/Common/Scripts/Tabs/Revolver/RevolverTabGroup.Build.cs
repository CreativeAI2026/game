using UnityEngine;

namespace CreativeAI.UI
{
    public sealed partial class RevolverTabGroup
    {
        public bool Build()
        {
            KillSelectionTween();
            ClearGeneratedItems();
            _built = false;
            _selectedIndex = -1;

            if (_entries == null || _entries.Count == 0)
                return FailBuild("Entry list is null or empty.");
            if (_itemPrefab == null)
                return FailBuild("Item Prefab is not assigned.");
            if (_itemRoot == null)
                return FailBuild("Item Root is not assigned.");

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i]?.Definition == null)
                {
                    ClearGeneratedItems();
                    return FailBuild($"Entry {i} has no TabDefinition.");
                }

                var item = Instantiate(_itemPrefab, _itemRoot, false);
                if (item == null)
                {
                    ClearGeneratedItems();
                    return FailBuild($"Failed to instantiate Entry {i}.");
                }

                if (!item.IsConfigured)
                {
                    DestroyGeneratedItem(item);
                    ClearGeneratedItems();
                    return FailBuild($"Entry {i} Item View has no configured TabButton.");
                }

                item.Bind(_entries[i].Definition, i, HandleItemClicked);
                _items.Add(item);
            }

            _built = true;
            int initialIndex = Mathf.Clamp(_initialIndex, 0, _entries.Count - 1);
            CompleteSelection(initialIndex);
            return true;
        }

        private bool FailBuild(string reason)
        {
            Debug.LogError($"{nameof(RevolverTabGroup)} '{name}' could not build: {reason}", this);
            return false;
        }

        private void ClearGeneratedItems()
        {
            UnbindItems();
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];
                if (item == null)
                    continue;

                DestroyGeneratedItem(item);
            }
            _items.Clear();
        }

        private void DestroyGeneratedItem(RevolverTabItemView item)
        {
            if (item == null)
                return;

            if (Application.isPlaying)
                Destroy(item.gameObject);
            else
                DestroyImmediate(item.gameObject);
        }

        private void UnbindItems()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null)
                    _items[i].Unbind();
            }
        }
    }
}
