using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// フィールドで拾い済みのアイテム(<see cref="FieldItemPickup.PickupId"/>)を覚えておくセッション常駐。
    /// シーンを移ってもフィールドに戻さず、セーブでは SaveService が保存し LoadService が復元する
    /// (「続きから」はセーブ時点に戻すので、セーブ後に拾ったものはまた置かれる)。
    /// 「はじめから」は常駐ごと作り直されるので空から始まる。
    /// </summary>
    public sealed class FieldItemManager : MonoBehaviour
    {
        /// <summary>セッション常駐のインスタンス。セッション開始前は null。</summary>
        public static FieldItemManager Instance { get; private set; }

        /// <summary>セッション常駐の拾い済みの記録を1つだけ生成する(既存ならそれを返す)。</summary>
        public static FieldItemManager EnsureResident()
        {
            if (Instance != null)
                return Instance;
            return new GameObject(nameof(FieldItemManager)).AddComponent<FieldItemManager>();
        }

        private readonly HashSet<string> _pickedIds = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>拾い済みか。識別子が空(未採番)なら常に false。</summary>
        public bool IsPicked(string pickupId) =>
            !string.IsNullOrEmpty(pickupId) && _pickedIds.Contains(pickupId);

        /// <summary>拾ったことを記録する。識別子が空(未採番)なら何もしない。</summary>
        public void MarkPicked(string pickupId)
        {
            if (!string.IsNullOrEmpty(pickupId))
                _pickedIds.Add(pickupId);
        }

        /// <summary>セーブ用の写し。並びを固定して差分を安定させる。</summary>
        public List<string> Snapshot() =>
            _pickedIds.OrderBy(id => id, System.StringComparer.Ordinal).ToList();

        /// <summary>記録をまるごと差し替える(null は空と同じ)。</summary>
        public void Restore(IEnumerable<string> pickupIds)
        {
            _pickedIds.Clear();
            if (pickupIds == null)
                return;
            foreach (var id in pickupIds)
                MarkPicked(id);
        }
    }
}
