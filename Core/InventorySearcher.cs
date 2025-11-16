using System;
using System.Collections.Generic;
using ItemStatsSystem;
using ItemWheel.Core.ItemSources;
using ItemWheel.Integration;
using UnityEngine;

namespace ItemWheel
{
    /// <summary>
    /// 搜索结果项，包含物品及其来源信息
    /// </summary>
    public class SearchResult
    {
        public Item Item;
        public int BackpackIndex;
        public Inventory Source;
        public bool IsFromSlot;
        public bool IsFromPet;
        public int SlotIndex;

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
    /// 通用库存搜索器，负责协调多个物品数据源。
    /// </summary>
    public static class InventorySearcher
    {
        /// <summary>
        /// 使用指定的搜索选项并行查询所有注册的数据源。
        /// </summary>
        public static List<SearchResult> SearchAll(InventorySearchOptions options)
        {
            var results = new List<SearchResult>();
            var addedItems = new HashSet<Item>();

            if (options == null || options.MatchPredicate == null)
            {
                return results;
            }

            foreach (var source in ItemSourceRegistry.Sources)
            {
                IEnumerable<SearchResult> sourceResults = null;
                try
                {
                    sourceResults = source.CollectItems(options);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ItemWheel] ItemSource '{source.Name}' failed: {ex.Message}");
                    continue;
                }

                if (sourceResults == null)
                {
                    continue;
                }

                foreach (var result in sourceResults)
                {
                    if (result?.Item == null)
                    {
                        continue;
                    }

                    if (!addedItems.Add(result.Item))
                    {
                        continue;
                    }

                    results.Add(result);
                }
            }

            return results;
        }

        /// <summary>
        /// 兼容旧接口：仅使用默认背包数据源。
        /// </summary>
        public static List<SearchResult> SearchAll(
            IEnumerable<Inventory> inventories,
            Func<Item, bool> matchPredicate,
            bool searchInSlots = true)
        {
            if (matchPredicate == null)
            {
                return new List<SearchResult>();
            }

            var settings = ItemWheelModSettings.CreateDefault();
            settings.SearchInSlots = searchInSlots;
            var options = new InventorySearchOptions(inventories, matchPredicate, settings);
            return SearchAll(options);
        }

        public static Dictionary<TKey, List<SearchResult>> SearchAndGroup<TKey>(
            InventorySearchOptions options,
            Func<Item, TKey> groupKeySelector)
            where TKey : notnull
        {
            var grouped = new Dictionary<TKey, List<SearchResult>>();
            if (groupKeySelector == null)
            {
                return grouped;
            }

            foreach (var result in SearchAll(options))
            {
                var key = groupKeySelector(result.Item);
                if (!grouped.TryGetValue(key, out var bucket))
                {
                    bucket = new List<SearchResult>();
                    grouped[key] = bucket;
                }
                bucket.Add(result);
            }

            return grouped;
        }

        /// <summary>
        /// 根据设置智能获取要搜索的背包列表
        /// </summary>
        public static List<Inventory> GetInventoriesToSearch(
            Inventory inventory,
            bool searchInPetInventory)
        {
            var inventories = new List<Inventory>();

            if (inventory != null)
            {
                inventories.Add(inventory);
            }

            if (searchInPetInventory && PetProxy.PetInventory != null)
            {
                inventories.Add(PetProxy.PetInventory);
            }

            return inventories;
        }

        /// <summary>
        /// 查找第一个匹配的物品。
        /// </summary>
        public static SearchResult FindFirst(InventorySearchOptions options)
        {
            var results = SearchAll(options);
            return results.Count > 0 ? results[0] : null;
        }
    }
}
