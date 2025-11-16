using System.Collections.Generic;
using ItemStatsSystem;
using UnityEngine;

namespace ItemWheel.Core.ItemSources.Sources
{
    /// <summary>
    /// 枚举主背包/宠物背包以及其中的容器。
    /// </summary>
    internal sealed class InventoryItemSource : IItemSource
    {
        public string Name => "DefaultInventory";

        public IEnumerable<ItemWheel.SearchResult> CollectItems(InventorySearchOptions options)
        {
            if (options?.Inventories == null || options.MatchPredicate == null)
            {
                yield break;
            }

            foreach (var inventory in options.Inventories)
            {
                if (inventory?.Content == null)
                {
                    continue;
                }

                bool isPetInventory = IsPetInventory(inventory);

                for (int backpackIndex = 0; backpackIndex < inventory.Content.Count; backpackIndex++)
                {
                    var item = inventory.Content[backpackIndex];
                    if (item == null)
                    {
                        continue;
                    }

                    if (SafeMatch(options, item))
                    {
                        yield return new ItemWheel.SearchResult(item, backpackIndex, inventory, false, isPetInventory);
                    }

                    if (!options.IncludeContainerSlots || item.Slots == null || item.Slots.Count == 0)
                    {
                        continue;
                    }

                    for (int slotIndex = 0; slotIndex < item.Slots.Count; slotIndex++)
                    {
                        var slot = item.Slots[slotIndex];
                        var slotItem = slot?.Content;
                        if (slotItem == null)
                        {
                            continue;
                        }

                        if (SafeMatch(options, slotItem))
                        {
                            yield return new ItemWheel.SearchResult(slotItem, backpackIndex, inventory, true, isPetInventory, slotIndex);
                        }
                    }
                }
            }
        }

        private static bool SafeMatch(InventorySearchOptions options, Item item)
        {
            try
            {
                return options.MatchPredicate(item);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ItemWheel] InventoryItemSource match failed: {ex.Message}");
                return false;
            }
        }

        private static bool IsPetInventory(Inventory inventory)
        {
            try
            {
                return inventory != null && inventory == PetProxy.PetInventory;
            }
            catch
            {
                return false;
            }
        }
    }
}
