using System;
using System.Collections.Generic;
using System.Linq;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace ItemWheel.Core.ItemSources.Sources
{
    /// <summary>
    /// 通过反射读取 Duckov_CashSlot 注册的额外槽位。
    /// </summary>
    internal sealed class CashSlotItemSource : IItemSource
    {
        private static Type _slotManagerType;
        private static Type _showInType;
        private static MethodInfo _getSlotsMethod;
        private static MethodInfo _getShowInMethod;
        private static MethodInfo _getInventoryIndexMethod;
        private static object _showInPetValue;
        private static bool _loggedMissingOwner;

        internal static bool IsSupported => EnsureBindings();

        public string Name => "CashSlot";

        public IEnumerable<ItemWheel.SearchResult> CollectItems(InventorySearchOptions options)
        {
            if (!IsSupported || options?.MatchPredicate == null)
            {
                if (!IsSupported)
                {
                    Debug.Log("[ItemWheel] CashSlotItemSource skipped: not supported (assembly not loaded yet?).");
                }
                yield break;
            }

            Item ownerItem = FindOwnerItem(options);
            if (ownerItem == null)
            {
                if (!_loggedMissingOwner)
                {
                    Debug.Log("[ItemWheel] CashSlotItemSource skipped: failed to resolve owner item.");
                    _loggedMissingOwner = true;
                }
                yield break;
            }
            _loggedMissingOwner = false;

            var registeredSlots = GetRegisteredSlots(ownerItem);
            if (registeredSlots == null || registeredSlots.Length == 0)
            {
                Debug.Log("[ItemWheel] CashSlotItemSource: no registered slots found on owner item.");
                yield break;
            }

            Debug.Log($"[ItemWheel] CashSlotItemSource: processing {registeredSlots.Length} registered slots (IncludeSlots={options.IncludeContainerSlots}).");

            var visitedItems = new HashSet<Item>();

            foreach (var slot in registeredSlots)
            {
                if (slot == null)
                {
                    continue;
                }
                var slotItem = slot.Content;
                if (slotItem == null)
                {
                    continue;
                }

                bool isPetSlot = IsPetSlot(slot);
                if (isPetSlot && !(options.Settings?.SearchInPetInventory ?? true))
                {
                    continue;
                }

                int backpackIndex = GetSlotInventoryIndex(slot);
                int slotIndex = slot.Master?.Slots?.list?.IndexOf(slot) ?? -1;
                var inventoryRef = ResolveInventoryReference(isPetSlot, options);

                Debug.Log($"[ItemWheel] CashSlotItemSource: slot '{slot.Key}' item '{slotItem.DisplayName}' inv={(slotItem.Inventory != null ? slotItem.Inventory.Content?.Count ?? 0 : 0)} slots={(slotItem.Slots != null ? slotItem.Slots.Count : 0)}");

                bool slotItemMatched = SafeMatch(options, slotItem);
                if (slotItemMatched)
                {
                    yield return new ItemWheel.SearchResult(slotItem, backpackIndex, inventoryRef, true, isPetSlot, slotIndex);
                }

                visitedItems.Clear();
                visitedItems.Add(slotItem);
                foreach (var nested in EnumerateNestedItems(slotItem, backpackIndex, inventoryRef, isPetSlot, options, visitedItems, slotItem.DisplayName))
                {
                    yield return nested;
                }
                visitedItems.Remove(slotItem);
            }
        }

        private static IEnumerable<ItemWheel.SearchResult> EnumerateNestedItems(
            Item container,
            int backpackIndex,
            Inventory inventoryRef,
            bool isPetSlot,
            InventorySearchOptions options,
            HashSet<Item> visited,
            string path)
        {
            if (!options.IncludeContainerSlots)
            {
                yield break;
            }

            if (container?.Slots != null && container.Slots.Count > 0)
            {
                for (int childIndex = 0; childIndex < container.Slots.Count; childIndex++)
                {
                    var childSlot = container.Slots[childIndex];
                    var childItem = childSlot?.Content;
                    if (childItem == null)
                    {
                        continue;
                    }

                    string currentPath = $"{path}/slot[{childIndex}]/{childItem.DisplayName}";
                    Debug.Log($"[ItemWheel] CashSlotItemSource: scanning nested slot item {currentPath}");

                    if (!visited.Add(childItem))
                    {
                        Debug.LogWarning($"[ItemWheel] CashSlotItemSource: detected circular reference at {currentPath}, skipping.");
                        continue;
                    }

                    if (!SafeMatch(options, childItem))
                    {
                        foreach (var nested in EnumerateNestedItems(childItem, backpackIndex, inventoryRef, isPetSlot, options, visited, currentPath))
                        {
                            yield return nested;
                        }
                        visited.Remove(childItem);
                        continue;
                    }

                    yield return new ItemWheel.SearchResult(childItem, backpackIndex, inventoryRef, true, isPetSlot, childIndex);
                    foreach (var nested in EnumerateNestedItems(childItem, backpackIndex, inventoryRef, isPetSlot, options, visited, currentPath))
                    {
                        yield return nested;
                    }

                    visited.Remove(childItem);
                }
            }

            var nestedInventory = container?.Inventory;
            if (nestedInventory != null && nestedInventory.Content != null && nestedInventory.Content.Count > 0)
            {
                for (int invIndex = 0; invIndex < nestedInventory.Content.Count; invIndex++)
                {
                    var nestedItem = nestedInventory.Content[invIndex];
                    if (nestedItem == null)
                    {
                        continue;
                    }

                    string currentPath = $"{path}/inv[{invIndex}]/{nestedItem.DisplayName}";
                    Debug.Log($"[ItemWheel] CashSlotItemSource: scanning nested inventory item {currentPath}");

                    if (!visited.Add(nestedItem))
                    {
                        Debug.LogWarning($"[ItemWheel] CashSlotItemSource: detected circular reference at {currentPath}, skipping.");
                        continue;
                    }

                    if (SafeMatch(options, nestedItem))
                    {
                        yield return new ItemWheel.SearchResult(nestedItem, backpackIndex, inventoryRef, true, isPetSlot, invIndex);
                    }

                    foreach (var deeper in EnumerateNestedItems(nestedItem, backpackIndex, inventoryRef, isPetSlot, options, visited, currentPath))
                    {
                        yield return deeper;
                    }

                    visited.Remove(nestedItem);
                }
            }
        }

        private static Slot[] GetRegisteredSlots(Item ownerItem)
        {
            try
            {
                var result = _getSlotsMethod?.Invoke(null, new object[] { ownerItem }) as Slot[];
                if (result != null)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ItemWheel] CashSlotItemSource failed to query slots: {ex.Message}");
            }
            return Array.Empty<Slot>();
        }

        private static bool IsPetSlot(Slot slot)
        {
            if (slot == null || _getShowInMethod == null || _showInType == null)
            {
                return false;
            }

            try
            {
                var value = _getShowInMethod.Invoke(null, new object[] { slot });
                return _showInPetValue != null && Equals(value, _showInPetValue);
            }
            catch
            {
                return false;
            }
        }

        private static int GetSlotInventoryIndex(Slot slot)
        {
            if (slot == null || _getInventoryIndexMethod == null)
            {
                return -1;
            }

            try
            {
                return (int)_getInventoryIndexMethod.Invoke(null, new object[] { slot });
            }
            catch
            {
                return -1;
            }
        }

        private static Inventory ResolveInventoryReference(bool isPetSlot, InventorySearchOptions options)
        {
            if (isPetSlot)
            {
                return PetProxy.PetInventory;
            }

            var preferred = options?.Inventories?.FirstOrDefault(inv => inv != null && inv != PetProxy.PetInventory);
            if (preferred != null)
            {
                return preferred;
            }

            try
            {
                return CharacterMainControl.Main?.CharacterItem?.Inventory;
            }
            catch
            {
                return null;
            }
        }

        private static bool SafeMatch(InventorySearchOptions options, Item item)
        {
            try
            {
                return options.MatchPredicate(item);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ItemWheel] CashSlotItemSource match failed: {ex.Message}");
                return false;
            }
        }

        private static Item FindOwnerItem(InventorySearchOptions options)
        {
            Item ownerItem = null;
            try
            {
                ownerItem = options.Character?.CharacterItem;
            }
            catch
            {
                ownerItem = null;
            }

            if (ownerItem != null)
            {
                return ownerItem;
            }

            try
            {
                ownerItem = CharacterMainControl.Main?.CharacterItem;
            }
            catch
            {
                ownerItem = null;
            }

            if (ownerItem != null)
            {
                return ownerItem;
            }

            if (options?.Inventories != null)
            {
                foreach (var inv in options.Inventories)
                {
                    if (inv?.AttachedToItem != null)
                    {
                        return inv.AttachedToItem;
                    }
                }
            }

            return null;
        }

        private static bool EnsureBindings()
        {
            if (_slotManagerType == null)
            {
                _slotManagerType = AccessTools.TypeByName("Duckov_CashSlot.SlotManager");
            }
            if (_slotManagerType == null)
            {
                _getSlotsMethod = null;
                _getShowInMethod = null;
                _getInventoryIndexMethod = null;
                Debug.Log("[ItemWheel] CashSlotItemSource: SlotManager type not found.");
                return false;
            }

            _getSlotsMethod ??= AccessTools.Method(_slotManagerType, "GetAllRegisteredSlotsInItem");
            _getShowInMethod ??= AccessTools.Method(_slotManagerType, "GetSlotShowIn");
            _getInventoryIndexMethod ??= AccessTools.Method(_slotManagerType, "GetSlotInventoryIndex");

            if (_showInType == null)
            {
                _showInType = AccessTools.TypeByName("Duckov_CashSlot.Data.ShowIn");
                if (_showInType != null)
                {
                    try
                    {
                        _showInPetValue = Enum.Parse(_showInType, "Pet");
                    }
                    catch
                    {
                        _showInPetValue = null;
                    }
                }
            }

            return _getSlotsMethod != null && _getShowInMethod != null && _getInventoryIndexMethod != null;
        }
    }
}
