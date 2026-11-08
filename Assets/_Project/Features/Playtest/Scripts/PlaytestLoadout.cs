using System.Collections;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Playtest
{
    /// <summary>
    /// ボス体験用ビルドで、シーンに入るたびにプレイヤーの持ち物を体験用にそろえる。
    /// - 全武器を持たせる
    /// - 回復用の食材を決まった数まで補充し、即時食材の枠(1/2/3キー)にセットする
    /// - セッションの最初の1回だけ、FieldDevBootstrap のテスト品で自動装着された装備品を外す(テスターが自分で選ぶため)
    /// 所持品の常駐(InventoryManager)はシーンの FieldDevBootstrap が作る前提。
    /// </summary>
    public sealed class PlaytestLoadout : MonoBehaviour
    {
        [Tooltip("即時食材の枠1〜3に入れる食材のキー(ItemCatalog の key)。上から順に枠へ入れる")]
        [SerializeField]
        private string[] _quickFoodKeys =
        {
            "apple_banana",
            "banana_white_rice",
            "miso_soup_white_rice",
        };

        [Tooltip("シーンに入るたびに、各食材をこの数まで補充する")]
        [SerializeField, Min(1)]
        private int _quickFoodCount = 3;

        // ドメインリロードを切った再生でも毎回外し直せるよう、再生開始時に戻す。
        private static bool _autoEquippedItemsRemoved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => _autoEquippedItemsRemoved = false;

        private IEnumerator Start()
        {
            // FieldDevBootstrap はエディターで dev_state.json を読み、Start の1フレーム後に武器を上書きする
            // (共有の dev_state.json は武器0本)。その後に持たせ直すため、2フレーム待つ。
            yield return null;
            yield return null;

            var inventory = InventoryManager.Instance;
            Assert.IsNotNull(
                inventory,
                $"{name}: InventoryManager がありません。シーンに FieldDevBootstrap を置いてください"
            );

            if (!_autoEquippedItemsRemoved)
            {
                RemoveAutoEquippedItems(inventory);
                _autoEquippedItemsRemoved = true;
            }
            RefillQuickFood(inventory);
            GiveAllWeapons();
        }

        private static void RemoveAutoEquippedItems(InventoryManager inventory)
        {
            foreach (var stack in inventory.GetItemsByCategory(ItemCategory.Equipment))
                inventory.SetEquipped(stack, false);
        }

        private void RefillQuickFood(InventoryManager inventory)
        {
            var catalog = ItemCatalog.Instance;
            Assert.IsNotNull(catalog, $"{name}: ItemCatalog が見つかりません");

            int slotCount = Mathf.Min(_quickFoodKeys.Length, inventory.GetQuickFoodSlots().Count);
            for (int slot = 0; slot < slotCount; slot++)
            {
                var food = catalog.GetItemByKey(_quickFoodKeys[slot]);
                if (food == null || food.category != ItemCategory.Food)
                {
                    Debug.LogError(
                        $"{name}: '{_quickFoodKeys[slot]}' は ItemCatalog の食材にありません",
                        this
                    );
                    continue;
                }

                int shortage = _quickFoodCount - inventory.GetItemCount(food);
                if (shortage > 0)
                    inventory.AddItem(food, shortage);

                var stack = inventory
                    .GetItemsByCategory(ItemCategory.Food)
                    .Find(item => item.Data == food);
                inventory.SetQuickFood(slot, stack);
            }
        }

        private void GiveAllWeapons()
        {
            var player = PlayerRegistry.Current;
            Assert.IsNotNull(player, $"{name}: プレイヤーが見つかりません");
            var weapons = player.GetComponent<WeaponManager>();
            foreach (string key in WeaponManager.WeaponKeys)
                weapons.GiveWeapon(key);
        }
    }
}
