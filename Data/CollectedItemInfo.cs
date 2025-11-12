using System.Collections.Generic;
using ItemStatsSystem;

namespace ItemWheel.Data
{
    /// <summary>
    /// 收集到的物品信息
    /// 包含物品及其完整位置信息
    /// </summary>
    public struct CollectedItemInfo
    {
        /// <summary>物品</summary>
        public Item Item;

        /// <summary>主要位置（第一个找到的位置）</summary>
        public ItemLocation Location;

        /// <summary>堆叠数量（主要用于手雷）</summary>
        public int StackCount;

        /// <summary>所有位置（手雷堆叠用，包含所有同类手雷的位置）</summary>
        public List<ItemLocation> AllLocations;

        // ==================== 便捷属性 ====================

        /// <summary>是否来自slot（容器内）</summary>
        public bool IsFromSlot => Location.IsFromSlot;

        /// <summary>是否来自宠物背包</summary>
        public bool IsFromPet => Location.IsFromPet;

        /// <summary>是否可拖拽（只有主背包顶层物品可拖拽）</summary>
        public bool IsDraggable => Location.IsDraggable;

        /// <summary>背包索引（便捷访问）</summary>
        public int BackpackIndex => Location.BackpackIndex;

        /// <summary>所属背包（便捷访问）</summary>
        public Inventory Inventory => Location.Inventory;

        // ==================== 构造函数 ====================

        /// <summary>
        /// 标准构造函数（单个物品）
        /// </summary>
        public CollectedItemInfo(Item item, ItemLocation location)
        {
            Item = item;
            Location = location;
            StackCount = 1;
            AllLocations = new List<ItemLocation> { location };
        }

        /// <summary>
        /// 堆叠构造函数（多个同类物品，主要用于手雷）
        /// </summary>
        public CollectedItemInfo(Item item, ItemLocation mainLocation, int stackCount, List<ItemLocation> allLocations)
        {
            Item = item;
            Location = mainLocation;
            StackCount = stackCount;
            AllLocations = allLocations;
        }

        /// <summary>
        /// 用于调试的字符串表示
        /// </summary>
        public override string ToString()
        {
            string itemName = Item?.DisplayName ?? "null";
            if (StackCount > 1)
            {
                return $"{itemName} x{StackCount} @ {Location}";
            }
            return $"{itemName} @ {Location}";
        }
    }
}
