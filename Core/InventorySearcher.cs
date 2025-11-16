using System;
using System.Collections.Generic;
using ItemStatsSystem;
using UnityEngine;

namespace ItemWheel
{
    /// <summary>
    /// 搜索结果项，包含物品及其来源信息
    /// </summary>
    public class SearchResult
    {
        /// <summary>物品</summary>
        public Item Item;

        /// <summary>在背包中的索引位置</summary>
        public int BackpackIndex;

        /// <summary>所属的背包</summary>
        public Inventory Source;

        /// <summary>是否来自容器插槽（true=容器内的物品，false=背包顶层物品）</summary>
        public bool IsFromSlot;

        /// <summary>是否来自宠物背包</summary>
        public bool IsFromPet;

        /// <summary>
        /// 🆕 容器槽位索引（仅当 IsFromSlot=true 时有效，-1表示顶层物品）
        /// </summary>
        public int SlotIndex;

        /// <summary>
        /// 构造函数
        /// </summary>
        public SearchResult(Item item, int backpackIndex, Inventory source, bool isFromSlot, bool isFromPet, int slotIndex = -1)
        {
            Item = item;
            BackpackIndex = backpackIndex;
            Source = source;
            IsFromSlot = isFromSlot;
            IsFromPet = isFromPet;
            SlotIndex = slotIndex;
        }
    }

    /// <summary>
    /// 通用库存搜索器，支持搜索多个Inventory（主背包、宠物背包等）
    /// 支持搜索背包中的容器（插槽）物品
    /// </summary>
    public static class InventorySearcher
    {
        /// <summary>
        /// 搜索多个Inventory，返回匹配的物品列表
        /// </summary>
        /// <param name="inventories">要搜索的背包列表</param>
        /// <param name="matchPredicate">匹配函数（返回true表示匹配）</param>
        /// <param name="searchInSlots">是否搜索容器内的物品</param>
        /// <returns>搜索结果列表</returns>
        public static List<SearchResult> SearchAll(
            IEnumerable<Inventory> inventories,
            Func<Item, bool> matchPredicate,
            bool searchInSlots = true)
        {
            var results = new List<SearchResult>();
            var addedItems = new HashSet<Item>(); // 防止重复添加同一物品

            if (inventories == null || matchPredicate == null)
            {
                return results;
            }

            foreach (var inventory in inventories)
            {
                if (inventory?.Content == null)
                    continue;

                bool isPetInventory = IsPetInventory(inventory);

                for (int backpackIndex = 0; backpackIndex < inventory.Content.Count; backpackIndex++)
                {
                    var item = inventory.Content[backpackIndex];
                    if (item == null)
                        continue;

                    // 1. 搜索背包顶层的物品
                    if (matchPredicate(item) && !addedItems.Contains(item))
                    {
                        results.Add(new SearchResult(item, backpackIndex, inventory, false, isPetInventory));
                        addedItems.Add(item);
                    }

                    // 2. 搜索容器内的物品（如果启用）
                    if (searchInSlots && item.Slots != null && item.Slots.Count > 0)
                    {
                        try
                        {
                            Debug.Log($"[InventorySearcher] 🔍 搜索容器: {item.DisplayName}, 插槽数={item.Slots.Count}, 背包={inventory.DisplayName}");

                            // 🆕 使用 for 循环记录槽位索引
                            for (int slotIndex = 0; slotIndex < item.Slots.Count; slotIndex++)
                            {
                                var slot = item.Slots[slotIndex];
                                if (slot?.Content == null)
                                {
                                    Debug.Log($"[InventorySearcher]   插槽[{slotIndex}]: 空");
                                    continue;
                                }

                                var slotItem = slot.Content;
                                bool matched = matchPredicate(slotItem);
                                bool alreadyAdded = addedItems.Contains(slotItem);

                                Debug.Log($"[InventorySearcher]   插槽[{slotIndex}]: {slotItem.DisplayName}, 匹配={matched}, 已添加={alreadyAdded}");

                                if (matched && !alreadyAdded)
                                {
                                    // 传递正确的 slotIndex
                                    results.Add(new SearchResult(slotItem, backpackIndex, inventory, true, isPetInventory, slotIndex));
                                    addedItems.Add(slotItem);
                                    Debug.Log($"[InventorySearcher]   ✅ 添加到结果: {slotItem.DisplayName}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[InventorySearcher] 搜索容器失败: {item?.DisplayName ?? "未知"}, {ex.Message}");
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// 搜索多个Inventory并按指定键分组
        /// 用于手雷堆叠（按TypeID分组）、子弹分类等场景
        /// </summary>
        /// <typeparam name="TKey">分组键类型</typeparam>
        /// <param name="inventories">要搜索的背包列表</param>
        /// <param name="matchPredicate">匹配函数</param>
        /// <param name="groupKeySelector">分组键选择器</param>
        /// <param name="searchInSlots">是否搜索容器内的物品</param>
        /// <returns>分组后的搜索结果</returns>
        public static Dictionary<TKey, List<SearchResult>> SearchAndGroup<TKey>(
            IEnumerable<Inventory> inventories,
            Func<Item, bool> matchPredicate,
            Func<Item, TKey> groupKeySelector,
            bool searchInSlots = true)
            where TKey : notnull
        {
            var results = new Dictionary<TKey, List<SearchResult>>();
            var allResults = SearchAll(inventories, matchPredicate, searchInSlots);

            foreach (var result in allResults)
            {
                var key = groupKeySelector(result.Item);

                if (!results.ContainsKey(key))
                {
                    results[key] = new List<SearchResult>();
                }

                results[key].Add(result);
            }

            return results;
        }

        /// <summary>
        /// 根据设置智能获取要搜索的背包列表
        /// </summary>
        /// <param name="inventory">主背包</param>
        /// <param name="searchInPetInventory">是否搜索宠物背包</param>
        /// <returns>背包列表</returns>
        public static List<Inventory> GetInventoriesToSearch(
            Inventory inventory,
            bool searchInPetInventory)
        {
            var inventories = new List<Inventory>();

            if (inventory != null)
            {
                inventories.Add(inventory);
            }

            // 如果启用了宠物背包搜索且宠物背包存在
            if (searchInPetInventory && PetProxy.PetInventory != null)
            {
                inventories.Add(PetProxy.PetInventory);
            }

            return inventories;
        }

        /// <summary>
        /// 判断是否是宠物背包
        /// </summary>
        private static bool IsPetInventory(Inventory inventory)
        {
            try
            {
                return inventory == PetProxy.PetInventory;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 查找第一个匹配的物品（用于子弹轮盘等场景）
        /// </summary>
        public static SearchResult FindFirst(
            IEnumerable<Inventory> inventories,
            Func<Item, bool> matchPredicate,
            bool searchInSlots = true)
        {
            var results = SearchAll(inventories, matchPredicate, searchInSlots);
            return results.Count > 0 ? results[0] : null;
        }
    }
}
