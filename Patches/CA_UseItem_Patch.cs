using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using UnityEngine;

namespace ItemWheel.Patches
{
    /// <summary>
    /// 修复官方Bug：使用带耐久的物品（如医疗包）后，物品会从原位置移动到玩家背包
    ///
    /// 问题根因：CA_UseItem.OnStop() 调用 PickupItem() 时，只会将物品拾取到玩家背包，
    /// 不会考虑物品原来在哪里（宠物背包、容器插槽等）
    ///
    /// 物品位置的两种情况：
    /// 1. 在 Inventory 中：item.InInventory != null
    /// 2. 在 Slot 中：item.PluggedIntoSlot != null（如战术背心的插槽）
    ///
    /// 容器层级示例：
    /// 玩家背包
    ///   └─ 战术背心 (容器)
    ///       └─ 插槽 (Slot)
    ///           └─ 医疗包 (Item) ← 使用后应该回到这里
    ///
    /// 解决方案：
    /// 1. OnStart Prefix：记录物品使用前的位置（Inventory+索引 或 Slot）
    /// 2. OnStop Postfix：如果物品还存在且位置改变，尝试放回原位置
    /// </summary>
    [HarmonyPatch(typeof(CA_UseItem))]
    public static class CA_UseItem_Patch
    {
        // 记录物品使用前的位置
        private class ItemOriginalLocation
        {
            // 位置类型：Inventory 或 Slot
            public enum LocationType { Inventory, Slot }
            public LocationType Type;

            // Inventory 位置信息
            public Inventory OriginalInventory;
            public int OriginalIndex;

            // Slot 位置信息（Slot 引用可能失效，需要通过 Master 和 Key 重新查找）
            public Item SlotMaster;  // 插槽所属的容器物品
            public string SlotKey;   // 插槽的键值
        }

        // 使用 Dictionary 来记录多个物品的位置（支持多人/多物品同时使用）
        private static readonly Dictionary<Item, ItemOriginalLocation> _itemLocations = new Dictionary<Item, ItemOriginalLocation>();

        /// <summary>
        /// 记录物品的原始位置（供轮盘系统调用）
        /// 必须在物品使用前（Detach前）调用
        /// </summary>
        public static void RecordItemLocation(Item item)
        {
            if (item == null) return;

            // 如果已经记录过，不重复记录
            if (_itemLocations.ContainsKey(item))
            {
                Debug.Log($"[ItemWheel] 🔍 物品位置已记录，跳过: {item.DisplayName}");
                return;
            }

            try
            {
                var location = new ItemOriginalLocation();

                // 优先检查是否在 Slot 中（容器插槽）
                if (item.PluggedIntoSlot != null)
                {
                    var slot = item.PluggedIntoSlot;
                    location.Type = ItemOriginalLocation.LocationType.Slot;
                    location.SlotMaster = slot.Master;
                    location.SlotKey = slot.Key;

                    Debug.Log($"[ItemWheel] 🔍 记录物品使用前位置(Slot): {item.DisplayName}, " +
                              $"容器={slot.Master?.DisplayName ?? "未知"}, 插槽Key={slot.Key}");
                }
                // 否则检查是否在 Inventory 中
                else if (item.InInventory != null)
                {
                    int index = item.InInventory.Content.IndexOf(item);
                    if (index < 0)
                    {
                        Debug.LogWarning($"[ItemWheel] ⚠️ 物品在背包中但找不到索引: {item.DisplayName}");
                        return;
                    }

                    location.Type = ItemOriginalLocation.LocationType.Inventory;
                    location.OriginalInventory = item.InInventory;
                    location.OriginalIndex = index;

                    Debug.Log($"[ItemWheel] 🔍 记录物品使用前位置(Inventory): {item.DisplayName}, " +
                              $"背包={item.InInventory.DisplayName}, 索引={index}");
                }
                else
                {
                    Debug.LogWarning($"[ItemWheel] ⚠️ 物品既不在Inventory也不在Slot中: {item.DisplayName}");
                    return;
                }

                _itemLocations[item] = location;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ItemWheel] RecordItemLocation 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// OnStart Prefix：备用记录（防止其他地方直接使用物品）
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("OnStart", MethodType.Normal)]
        private static void OnStart_Prefix(CA_UseItem __instance)
        {
            try
            {
                // 通过反射获取私有字段 item
                var itemField = typeof(CA_UseItem).GetField("item",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);

                if (itemField == null) return;

                var item = itemField.GetValue(__instance) as Item;
                if (item == null) return;

                // 尝试记录位置（如果已记录则跳过）
                RecordItemLocation(item);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ItemWheel] OnStart_Prefix 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// OnStop Postfix：尝试将物品放回原位置
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch("OnStop", MethodType.Normal)]
        private static void OnStop_Postfix(CA_UseItem __instance)
        {
            try
            {
                // 通过反射获取私有字段 item
                var itemField = typeof(CA_UseItem).GetField("item",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);

                if (itemField == null) return;

                var item = itemField.GetValue(__instance) as Item;
                if (item == null || item.IsBeingDestroyed) return;

                // 查找记录的原始位置
                if (!_itemLocations.TryGetValue(item, out var location))
                {
                    Debug.Log($"[ItemWheel] ⏭️ 没有记录的原始位置: {item.DisplayName}");
                    return;
                }

                // 清理记录
                _itemLocations.Remove(item);

                // 检查物品当前位置是否已经正确
                if (IsItemInCorrectLocation(item, location))
                {
                    Debug.Log($"[ItemWheel] ✅ 物品已在正确位置: {item.DisplayName}");
                    return;
                }

                // 尝试恢复到原位置
                Debug.Log($"[ItemWheel] 🔄 尝试将物品移回原位置: {item.DisplayName}, 类型={location.Type}");

                bool success = false;
                if (location.Type == ItemOriginalLocation.LocationType.Slot)
                {
                    success = TryMoveItemToSlot(item, location.SlotMaster, location.SlotKey);
                }
                else
                {
                    success = TryMoveItemToInventory(item, location.OriginalInventory, location.OriginalIndex);
                }

                if (success)
                {
                    Debug.Log($"[ItemWheel] ✅ 成功将物品移回原位置: {item.DisplayName}");
                }
                else
                {
                    Debug.LogWarning($"[ItemWheel] ❌ 无法将物品移回原位置: {item.DisplayName}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ItemWheel] OnStop_Postfix 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查物品是否已经在正确的位置
        /// </summary>
        private static bool IsItemInCorrectLocation(Item item, ItemOriginalLocation location)
        {
            if (location.Type == ItemOriginalLocation.LocationType.Slot)
            {
                // 检查是否在原插槽中
                if (item.PluggedIntoSlot != null &&
                    item.PluggedIntoSlot.Master == location.SlotMaster &&
                    item.PluggedIntoSlot.Key == location.SlotKey)
                {
                    return true;
                }
            }
            else
            {
                // 检查是否在原背包中
                if (item.InInventory == location.OriginalInventory)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 尝试将物品移动到指定插槽
        /// </summary>
        private static bool TryMoveItemToSlot(Item item, Item slotMaster, string slotKey)
        {
            if (item == null || slotMaster == null || string.IsNullOrEmpty(slotKey))
            {
                Debug.LogWarning($"[ItemWheel] ❌ TryMoveItemToSlot: 参数无效");
                return false;
            }

            // 检查容器物品是否还存在
            if (slotMaster.IsBeingDestroyed)
            {
                Debug.LogWarning($"[ItemWheel] ❌ 容器已被销毁: {slotMaster.DisplayName}");
                return false;
            }

            // 通过反射获取 SlotCollection
            var slotsField = typeof(Item).GetField("slots",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);

            if (slotsField == null)
            {
                Debug.LogWarning($"[ItemWheel] ❌ 无法获取容器的 slots 字段");
                return false;
            }

            var slotCollection = slotsField.GetValue(slotMaster) as SlotCollection;
            if (slotCollection == null)
            {
                Debug.LogWarning($"[ItemWheel] ❌ 容器没有 SlotCollection: {slotMaster.DisplayName}");
                return false;
            }

            // 查找目标插槽
            Slot targetSlot = null;
            foreach (var slot in slotCollection)
            {
                if (slot != null && slot.Key == slotKey)
                {
                    targetSlot = slot;
                    break;
                }
            }

            if (targetSlot == null)
            {
                Debug.LogWarning($"[ItemWheel] ❌ 找不到插槽: Key={slotKey}");
                return false;
            }

            // 先从当前位置移除
            item.Detach();

            // 尝试插入插槽
            Item unpluggedItem;
            bool success = targetSlot.Plug(item, out unpluggedItem);

            if (success)
            {
                Debug.Log($"[ItemWheel] ✅ 成功插回插槽: {slotKey}");

                // 如果有被挤出的物品，尝试放到玩家背包
                if (unpluggedItem != null)
                {
                    Debug.Log($"[ItemWheel] 📦 插槽中的物品被挤出: {unpluggedItem.DisplayName}");
                    var playerInventory = CharacterMainControl.Main?.CharacterItem?.Inventory;
                    if (playerInventory != null)
                    {
                        playerInventory.AddItem(unpluggedItem);
                    }
                }

                return true;
            }
            else
            {
                Debug.LogWarning($"[ItemWheel] ❌ 无法插入插槽（可能不满足Tag要求）");

                // 放回玩家背包
                var playerInventory = CharacterMainControl.Main?.CharacterItem?.Inventory;
                if (playerInventory != null)
                {
                    playerInventory.AddItem(item);
                }

                return false;
            }
        }

        /// <summary>
        /// 尝试将物品移动到指定背包的指定位置
        /// </summary>
        private static bool TryMoveItemToInventory(Item item, Inventory targetInventory, int preferredIndex)
        {
            if (item == null || targetInventory == null)
            {
                Debug.LogWarning($"[ItemWheel] ❌ TryMoveItemToInventory: 参数无效");
                return false;
            }

            // 检查背包是否还存在（通过检查 gameObject 是否有效）
            if (targetInventory == null || targetInventory.gameObject == null)
            {
                Debug.LogWarning($"[ItemWheel] ❌ 目标背包已被销毁");
                return false;
            }

            // 1. 先从当前位置移除
            item.Detach();

            // 2. 尝试原索引位置
            if (preferredIndex >= 0 && preferredIndex < targetInventory.Content.Count)
            {
                if (targetInventory.Content[preferredIndex] == null)
                {
                    targetInventory.AddAt(item, preferredIndex);
                    Debug.Log($"[ItemWheel] ✅ 放回原索引: {preferredIndex}");
                    return true;
                }
            }

            // 3. 原位置被占用，尝试找到第一个空位
            int emptyIndex = targetInventory.Content.FindIndex(x => x == null);
            if (emptyIndex >= 0)
            {
                targetInventory.AddAt(item, emptyIndex);
                Debug.Log($"[ItemWheel] ✅ 放到空位: {emptyIndex}");
                return true;
            }

            // 4. 没有空位，尝试添加到末尾（如果背包允许扩展）
            try
            {
                targetInventory.AddItem(item);
                Debug.Log($"[ItemWheel] ✅ 添加到末尾");
                return true;
            }
            catch
            {
                // 背包已满，无法添加
                Debug.LogWarning($"[ItemWheel] ❌ 背包已满，无法添加");

                // 放回玩家背包
                var playerInventory = CharacterMainControl.Main?.CharacterItem?.Inventory;
                if (playerInventory != null)
                {
                    playerInventory.AddItem(item);
                    Debug.Log($"[ItemWheel] ⚠️ 放回玩家背包");
                }

                return false;
            }
        }
    }
}
