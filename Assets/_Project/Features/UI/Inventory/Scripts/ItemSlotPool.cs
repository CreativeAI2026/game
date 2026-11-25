using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 持ち物の一覧のスロットを使い回す。空きは非アクティブにしておき、足りなければ Prefab から作る。
    /// 最初に使うとき、並べる親に元から置いてあるスロットも空きとして取り込む。
    /// </summary>
    internal sealed class ItemSlotPool
    {
        private readonly Transform _root;
        private readonly GameObject _prefab;
        private readonly Object _owner; // エラーの出どころ
        private readonly List<ItemSlot> _slots = new();
        private bool _initialized;

        public ItemSlotPool(Transform root, GameObject prefab, Object owner)
        {
            _root = root;
            _prefab = prefab;
            _owner = owner;
        }

        /// <summary>作ったスロット全部(表示中と空きの両方)。</summary>
        public IReadOnlyList<ItemSlot> All => _slots;

        /// <summary>空きのスロットを返す。無ければ作る。Prefab にスロットが無ければ null。</summary>
        public ItemSlot Get()
        {
            Initialize();

            foreach (var slot in _slots)
            {
                // 表示中のスロットは必ずアクティブなので、非アクティブなら空き
                if (slot != null && !slot.gameObject.activeSelf)
                    return slot;
            }

            var createdObject = Object.Instantiate(_prefab, _root, false);
            if (!createdObject.TryGetComponent(out ItemSlot createdSlot))
            {
                Debug.LogError(
                    $"{nameof(ItemListView)} '{_owner.name}' のSlot Prefab '{_prefab.name}'に{nameof(ItemSlot)}がありません。Prefabのルートへ追加してください。",
                    _prefab
                );
                Object.Destroy(createdObject);
                return null;
            }

            _slots.Add(createdSlot);
            return createdSlot;
        }

        /// <summary>スロットを空きに戻す(中身を消して隠す)。</summary>
        public void Return(ItemSlot slot)
        {
            if (slot == null)
                return;

            slot.transform.DOKill();
            slot.Deselect();
            slot.SetCraftAssigned(false);
            slot.SetItem(null);
            slot.transform.localScale = Vector3.one;
            slot.gameObject.SetActive(false);
        }

        /// <summary>並べる親に元から置いてあるスロットを空きとして取り込み、隠す。2回目以降は何もしない。</summary>
        public void Initialize()
        {
            if (_initialized || _root == null)
                return;

            _initialized = true;
            for (int i = 0; i < _root.childCount; i++)
            {
                var slot = _root.GetChild(i).GetComponent<ItemSlot>();
                if (slot == null || _slots.Contains(slot))
                    continue;

                _slots.Add(slot);
                Return(slot);
            }
        }
    }
}
