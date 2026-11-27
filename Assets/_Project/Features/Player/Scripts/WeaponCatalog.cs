using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 武器の定義(<see cref="WeaponData"/>)の一覧。並び順は <see cref="WeaponManager.WeaponKeys"/> と同じ(0 剣 / 1 弓 / 2 鎌 / 3 ハンマー)。
    /// 武器は所持品の外なので、ItemCatalog とは分けて Resources に置く。
    /// 補正の値・アイコン・名前はここから引き、プレイヤーの見た目(PlayerWeapons)には頼らない。
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponCatalog", menuName = "Scriptable Objects/WeaponCatalog")]
    public sealed class WeaponCatalog : ScriptableObject
    {
        private static readonly int[] WeaponIds = { 1001, 1002, 1003, 1004 };

        private static WeaponCatalog _instance;
        private static WeaponCatalog _injected;

        /// <summary>Resources の WeaponCatalog。テストで差し替えたときはそれを返す。</summary>
        public static WeaponCatalog Instance
        {
            get
            {
                if (_injected != null)
                    return _injected;
                if (_instance == null)
                    _instance = Resources.Load<WeaponCatalog>("WeaponCatalog");
                return _instance;
            }
        }

        /// <summary>テスト専用: <see cref="Instance"/> を指定の一覧に差し替える。null で解除する。</summary>
        public static void InjectForTests(IReadOnlyList<WeaponData> weapons)
        {
            if (_injected != null)
                DestroyImmediate(_injected);
            _injected = null;
            if (weapons == null)
                return;
            _injected = CreateInstance<WeaponCatalog>();
            _injected._weapons = new List<WeaponData>(weapons).ToArray();
        }

        [Tooltip("並び順 = WeaponManager.WeaponKeys(0 剣 / 1 弓 / 2 鎌 / 3 ハンマー)")]
        [SerializeField]
        private WeaponData[] _weapons = Array.Empty<WeaponData>();

        /// <summary>index(WeaponManager.WeaponKeys の並び)の武器の定義。範囲外・未設定なら null。</summary>
        public WeaponData Get(int index) =>
            index >= 0 && index < _weapons.Length ? _weapons[index] : null;

        /// <summary>武器 ID の index。無ければ -1。</summary>
        public int IndexOf(int weaponId) =>
            Array.FindIndex(_weapons, weapon => weapon != null && weapon.id == weaponId);

#if UNITY_EDITOR
        /// <summary>並び順の契約(WeaponManager.WeaponKeys)と武器 ID が食い違っていたら、編集時に警告する。</summary>
        private void OnValidate()
        {
            var keys = WeaponManager.WeaponKeys;
            if (_weapons.Length != keys.Count)
                Debug.LogWarning(
                    $"{name}: 武器の一覧は {keys.Count} 枠(並び順 {string.Join(" / ", keys)})にしてください",
                    this
                );
            int count = Mathf.Min(_weapons.Length, keys.Count);
            for (int i = 0; i < count; i++)
            {
                if (_weapons[i] != null && _weapons[i].id != WeaponIds[i])
                    Debug.LogWarning(
                        $"{name}: 武器の一覧 [{i}] は「{keys[i]}」の枠ですが、武器IDが「{_weapons[i].id}」です（期待値: {WeaponIds[i]}）",
                        this
                    );
            }
        }
#endif
    }
}
