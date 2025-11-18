using System;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using ItemWheel.Data;
using UnityEngine;

namespace ItemWheel.Handlers
{
    /// <summary>
    /// 枪械轮盘处理器，负责主/副手武器交换
    /// </summary>
    public sealed class GunHandler : IItemHandler
    {
        public ItemWheelSystem.ItemWheelCategory Category => ItemWheelSystem.ItemWheelCategory.Gun;

        public void UseItem(Item item, CharacterMainControl character, CategoryWheel wheel)
        {
            if (item == null || character == null || wheel == null)
            {
                return;
            }

            var targetSlotType = wheel.TargetGunSlot ?? ItemWheelSystem.GunSlotTarget.Primary;
            SwapWeapon(item, character, wheel, targetSlotType);
        }

        public void OnItemSelected(Item item, int index, CategoryWheel wheel)
        {
            // 枪械轮盘在确认时一次性完成交换，不需要额外处理
        }

        public void OnWheelShown(CategoryWheel wheel)
        {
            if (wheel?.Slots == null || wheel.TargetGunSlot == null)
            {
                return;
            }

            try
            {
                var character = CharacterMainControl.Main;
                var targetSlotItem = GetSlot(character, wheel.TargetGunSlot.Value)?.Content;
                if (targetSlotItem == null)
                {
                    wheel.LastConfirmedIndex = GetFirstNonEmptyIndex(wheel);
                    return;
                }

                int idx = Array.IndexOf(wheel.Slots, targetSlotItem);
                if (idx >= 0)
                {
                    wheel.LastConfirmedIndex = idx;
                    wheel.LastSelectedItem = targetSlotItem;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GunHandler] OnWheelShown failed: {ex.Message}");
            }
        }

        public int GetPreferredIndex(CategoryWheel wheel)
        {
            if (wheel?.Slots == null)
            {
                return -1;
            }

            try
            {
                if (wheel.TargetGunSlot != null)
                {
                    var slotItem = GetSlot(CharacterMainControl.Main, wheel.TargetGunSlot.Value)?.Content;
                    if (slotItem != null)
                    {
                        int idx = Array.IndexOf(wheel.Slots, slotItem);
                        if (idx >= 0)
                        {
                            return idx;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GunHandler] GetPreferredIndex failed: {ex.Message}");
            }

            return GetFirstNonEmptyIndex(wheel);
        }

        private static void SwapWeapon(Item newItem, CharacterMainControl character, CategoryWheel wheel, ItemWheelSystem.GunSlotTarget targetSlotType)
        {
            var targetSlot = GetSlot(character, targetSlotType);
            if (targetSlot == null)
            {
                return;
            }

            var sourceSlotType = DetermineSlotType(character, newItem);
            var sourceSlot = sourceSlotType.HasValue ? GetSlot(character, sourceSlotType.Value) : null;
            var originLocation = FindItemLocation(wheel, newItem);

            if (targetSlot.Content == newItem)
            {
                return;
            }

            try
            {
                if (!targetSlot.Plug(newItem, out var removedItem))
                {
                    Debug.LogWarning("[GunHandler] Plug target slot failed");
                    return;
                }

                if (removedItem != null && removedItem != newItem)
                {
                    if (sourceSlot != null)
                    {
                        if (!sourceSlot.Plug(removedItem, out var displaced) && displaced != null)
                        {
                            PlaceItemBack(displaced, originLocation, character);
                        }
                    }
                    else
                    {
                        PlaceItemBack(removedItem, originLocation, character);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GunHandler] SwapWeapon failed: {ex.Message}");
            }
        }

        private static Slot GetSlot(CharacterMainControl character, ItemWheelSystem.GunSlotTarget target)
        {
            if (character == null)
            {
                return null;
            }

            return target switch
            {
                ItemWheelSystem.GunSlotTarget.Primary => character.PrimWeaponSlot(),
                ItemWheelSystem.GunSlotTarget.Secondary => character.SecWeaponSlot(),
                _ => null
            };
        }

        private static ItemWheelSystem.GunSlotTarget? DetermineSlotType(CharacterMainControl character, Item item)
        {
            if (character == null || item == null)
            {
                return null;
            }

            if (character.PrimWeaponSlot()?.Content == item)
            {
                return ItemWheelSystem.GunSlotTarget.Primary;
            }

            if (character.SecWeaponSlot()?.Content == item)
            {
                return ItemWheelSystem.GunSlotTarget.Secondary;
            }

            return null;
        }

        private static ItemLocation? FindItemLocation(CategoryWheel wheel, Item item)
        {
            if (wheel?.DisplayedItems == null || item == null)
            {
                return null;
            }

            foreach (var info in wheel.DisplayedItems)
            {
                if (info.Item == item)
                {
                    return info.Location;
                }
            }

            return null;
        }

        private static bool PlaceItemBack(Item item, ItemLocation? location, CharacterMainControl character)
        {
            if (item == null)
            {
                return false;
            }

            if (location.HasValue)
            {
                var info = location.Value;
                var inventory = info.Inventory ?? character?.CharacterItem?.Inventory;
                if (inventory != null)
                {
                    try
                    {
                        item.Detach();
                        if (info.BackpackIndex >= 0 &&
                            info.BackpackIndex < inventory.Content.Count &&
                            inventory.Content[info.BackpackIndex] == null &&
                            inventory.AddAt(item, info.BackpackIndex))
                        {
                            return true;
                        }

                        if (inventory.AddItem(item))
                        {
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[GunHandler] PlaceItemBack failed: {ex.Message}");
                    }
                }
            }

            return TryAddToMainInventory(item, character);
        }

        private static bool TryAddToMainInventory(Item item, CharacterMainControl character)
        {
            var inventory = character?.CharacterItem?.Inventory;
            if (inventory == null)
            {
                return false;
            }

            try
            {
                item.Detach();
                return inventory.AddItem(item);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GunHandler] AddItem failed: {ex.Message}");
                return false;
            }
        }

        private static int GetFirstNonEmptyIndex(CategoryWheel wheel)
        {
            if (wheel?.Slots == null)
            {
                return -1;
            }

            for (int i = 0; i < wheel.Slots.Length; i++)
            {
                if (i == 8)
                {
                    continue;
                }

                if (wheel.Slots[i] != null)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
