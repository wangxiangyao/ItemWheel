using System;
using System.Collections.Generic;
using System.Linq;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using ItemWheel.Core.ItemSources;
using ItemWheel.Data;
using ItemWheel.Integration;
using UnityEngine;

namespace ItemWheel.Core
{
    /// <summary>
    /// 物品收集器
    /// 负责从多个背包（主背包、宠物背包、容器）中搜索并收集物品
    /// 收集阶段不限制数量，由显示层决定显示多少
    /// </summary>
    public static class ItemCollector
    {
        /// <summary>
        /// 收集指定类别的所有物品
        /// </summary>
        /// <param name="mainInventory">主背包</param>
        /// <param name="category">物品类别</param>
        /// <param name="matchPredicate">匹配函数</param>
        /// <param name="settings">配置</param>
        /// <param name="enableStacking">是否启用堆叠（手雷、子弹需要堆叠）</param>
        /// <returns>收集到的所有物品列表（无数量限制）</returns>
        public static List<CollectedItemInfo> Collect(
            Inventory mainInventory,
            CharacterMainControl character,
            ItemWheelSystem.ItemWheelCategory category,
            Func<Item, bool> matchPredicate,
            ItemWheelModSettings settings,
            bool enableStacking)
        {
            if (mainInventory?.Content == null || matchPredicate == null)
            {
                return new List<CollectedItemInfo>();
            }

            // 根据是否堆叠选择收集方式
            if (enableStacking)
            {
                return CollectWithStacking(mainInventory, matchPredicate, settings, character);
            }
            else
            {
                return CollectNormal(mainInventory, matchPredicate, settings, character);
            }
        }

        /// <summary>
        /// 收集物品（堆叠模式 - 按 TypeID 分组）
        /// 用于手雷、子弹等需要堆叠显示的类别
        /// </summary>
        private static List<CollectedItemInfo> CollectWithStacking(
            Inventory mainInventory,
            Func<Item, bool> matchPredicate,
            ItemWheelModSettings settings,
            CharacterMainControl character)
        {
            var result = new List<CollectedItemInfo>();
            var addedItems = new HashSet<Item>();

            // 1. 获取要搜索的背包列表
            var inventories = InventorySearcher.GetInventoriesToSearch(
                mainInventory,
                settings.SearchInPetInventory
            );

            Debug.Log($"[ItemCollector] 堆叠搜索 - 背包数量: {inventories.Count}, 搜索容器: {settings.SearchInSlots}, 搜索宠物: {settings.SearchInPetInventory}");

            // 2. 搜索所有背包
            var options = new InventorySearchOptions(
                inventories,
                matchPredicate,
                settings,
                character
            );
            var searchResults = InventorySearcher.SearchAll(options);

            Debug.Log($"[ItemCollector] 找到 {searchResults.Count} 个物品（堆叠前）");

            // 3. 按 TypeID 分组堆叠
            var itemGroups = new Dictionary<string, List<SearchResult>>();

            foreach (var searchResult in searchResults)
            {
                if (searchResult.Item == null || addedItems.Contains(searchResult.Item))
                    continue;

                string typeId = searchResult.Item.TypeID.ToString();
                if (!itemGroups.ContainsKey(typeId))
                {
                    itemGroups[typeId] = new List<SearchResult>();
                }

                itemGroups[typeId].Add(searchResult);
                addedItems.Add(searchResult.Item);
            }

            Debug.Log($"[ItemCollector] 堆叠后分组数量: {itemGroups.Count}");

            // 4. 为每组创建堆叠的 CollectedItemInfo（不限制数量）
            foreach (var group in itemGroups.OrderBy(g => g.Value[0].BackpackIndex))
            {
                var items = group.Value;
                if (items.Count == 0) continue;

                // 按背包索引排序（保持顺序一致性）
                items = items.OrderBy(i => i.BackpackIndex).ToList();

                // 创建位置列表
                var allLocations = items.Select(i => CreateLocation(i)).ToList();

                // 第一个作为代表
                var firstItem = items[0];
                var mainLocation = CreateLocation(firstItem);

                result.Add(new CollectedItemInfo(
                    firstItem.Item,
                    mainLocation,
                    items.Count,
                    allLocations
                ));

                Debug.Log($"[ItemCollector] 堆叠: {firstItem.Item.DisplayName} x{items.Count} @ {mainLocation}");
            }

            return result;
        }

        /// <summary>
        /// 收集物品（普通模式 - 不堆叠）
        /// 用于医疗品、刺激物、食物等不需要堆叠的类别
        /// </summary>
        private static List<CollectedItemInfo> CollectNormal(
            Inventory mainInventory,
            Func<Item, bool> matchPredicate,
            ItemWheelModSettings settings,
            CharacterMainControl character)
        {
            var result = new List<CollectedItemInfo>();
            var addedItems = new HashSet<Item>();

            // 1. 获取要搜索的背包列表
            var inventories = InventorySearcher.GetInventoriesToSearch(
                mainInventory,
                settings.SearchInPetInventory
            );

            Debug.Log($"[ItemCollector] 普通搜索 - 背包数量: {inventories.Count}, 搜索容器: {settings.SearchInSlots}, 搜索宠物: {settings.SearchInPetInventory}");

            // 2. 搜索所有背包
            var options = new InventorySearchOptions(
                inventories,
                matchPredicate,
                settings,
                character
            );
            var searchResults = InventorySearcher.SearchAll(options);

            Debug.Log($"[ItemCollector] 找到 {searchResults.Count} 个物品");

            // 3. 转换为 CollectedItemInfo（不限制数量）
            foreach (var searchResult in searchResults)
            {
                if (searchResult.Item == null || addedItems.Contains(searchResult.Item))
                    continue;

                var location = CreateLocation(searchResult);

                result.Add(new CollectedItemInfo(searchResult.Item, location));
                addedItems.Add(searchResult.Item);

                Debug.Log($"[ItemCollector] {searchResult.Item.DisplayName} @ {location}");
            }

            return result;
        }

        /// <summary>
        /// 收集近战武器（包括装备槽中的）
        /// </summary>
        public static List<CollectedItemInfo> CollectMelee(
            Inventory mainInventory,
            CharacterMainControl character,
            Func<Item, bool> matchPredicate,
            ItemWheelModSettings settings)
        {
            var result = CollectNormal(mainInventory, matchPredicate, settings, character);
            var addedItems = new HashSet<Item>(result.Select(r => r.Item));

            // 近战槽中的武器也纳入候选
            try
            {
                var meleeSlot = character?.MeleeWeaponSlot();
                var slotItem = meleeSlot?.Content;

                if (slotItem != null && matchPredicate(slotItem) && !addedItems.Contains(slotItem))
                {
                    // 近战槽物品：特殊位置（BackpackIndex=-1 表示装备槽）
                    var location = new ItemLocation(mainInventory, -1, -1);
                    result.Add(new CollectedItemInfo(slotItem, location));
                    Debug.Log($"[ItemCollector] 添加近战槽物品: {slotItem.DisplayName}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ItemCollector] 收集近战槽物品失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 收集枪械武器（包含主副手槽中的武器）
        /// </summary>
        public static List<CollectedItemInfo> CollectGuns(
            Inventory mainInventory,
            CharacterMainControl character,
            Func<Item, bool> matchPredicate,
            ItemWheelModSettings settings)
        {
            var result = new List<CollectedItemInfo>();

            var inventories = InventorySearcher.GetInventoriesToSearch(
                mainInventory,
                settings.SearchInPetInventory
            );

            var options = new InventorySearchOptions(
                inventories,
                matchPredicate,
                settings,
                character
            );

            var searchResults = InventorySearcher.SearchAll(options);

            foreach (var searchResult in searchResults)
            {
                if (searchResult.Item == null || !HasAmmo(searchResult.Item))
                {
                    continue;
                }

                var location = CreateLocation(searchResult);
                result.Add(new CollectedItemInfo(searchResult.Item, location));
            }

            if (settings?.IncludeEquippedGuns == true)
            {
                TryAddWeaponSlotItem(character?.PrimWeaponSlot(), mainInventory, matchPredicate, result);
                TryAddWeaponSlotItem(character?.SecWeaponSlot(), mainInventory, matchPredicate, result);
            }

            return result;
        }

        private static void TryAddWeaponSlotItem(
            Slot slot,
            Inventory fallbackInventory,
            Func<Item, bool> matchPredicate,
            List<CollectedItemInfo> result)
        {
            var slotItem = slot?.Content;
            if (slotItem == null)
            {
                return;
            }

            if (!matchPredicate(slotItem) || !HasAmmo(slotItem))
            {
                return;
            }

            var location = new ItemLocation(fallbackInventory, -1, -1);
            result.Add(new CollectedItemInfo(slotItem, location));
        }

        private static bool HasAmmo(Item item)
        {
            var gunSetting = item?.GetComponent<ItemSetting_Gun>();
            if (gunSetting == null)
            {
                return false;
            }

            return gunSetting.BulletCount > 0;
        }

        /// <summary>
        /// 从 SearchResult 创建 ItemLocation
        /// </summary>
        private static ItemLocation CreateLocation(SearchResult searchResult)
        {
            // 🆕 使用 SearchResult 中记录的精确 SlotIndex
            return new ItemLocation(
                searchResult.Source,
                searchResult.BackpackIndex,
                searchResult.SlotIndex  // -1 表示顶层，>=0 表示容器槽位
            );
        }

        /// <summary>
        /// 判断物品是否可堆叠显示
        /// </summary>
        public static bool ShouldStack(ItemWheelSystem.ItemWheelCategory category)
        {
            return category switch
            {
                ItemWheelSystem.ItemWheelCategory.Explosive => true,  // 手雷堆叠
                // 子弹不在 ItemWheelCategory 里，由 AmmoWheelSystem 单独处理
                _ => false
            };
        }
    }
}
