using System.Collections.Generic;
using ItemStatsSystem;
using QuickWheel.Core;
using QuickWheel.UI;

namespace ItemWheel.Data
{
    /// <summary>
    /// 类别轮盘上下文
    /// 保存单个类别轮盘的所有状态和数据
    /// </summary>
    public sealed class CategoryWheel
    {
        /// <summary>轮盘类别</summary>
        public ItemWheelSystem.ItemWheelCategory Category;

        /// <summary>轮盘实例</summary>
        public Wheel<Item> Wheel;

        /// <summary>输入处理器引用</summary>
        public QuickWheel.Input.MouseWheelInput Input;

        /// <summary>View引用（用于设置中心位置）</summary>
        public DefaultWheelView<Item> View;

        // ==================== 显示相关 ====================

        /// <summary>当前显示在轮盘上的物品槽位（最多9个，索引8是中心）</summary>
        public Item[] Slots;

        /// <summary>上次确认的选中索引</summary>
        public int LastConfirmedIndex;

        /// <summary>是否首次加载（用于从官方快捷栏同步选中）</summary>
        public bool IsFirstLoad;

        // ==================== 数据管理（新架构）====================

        /// <summary>
        /// 🆕 完整的物品列表（所有匹配的物品，无数量限制）
        /// 用于动态补位：当显示的物品被消耗后，从这里补充
        /// </summary>
        public List<CollectedItemInfo> AllItems;

        /// <summary>
        /// 🆕 当前显示的物品信息（最多8个）
        /// 与 Slots 数组对应
        /// </summary>
        public List<CollectedItemInfo> DisplayedItems;

        // ==================== 映射和标记（保留兼容）====================

        /// <summary>轮盘位置[0-7] → 背包位置</summary>
        public int[] WheelToBackpackMapping;

        /// <summary>背包位置 → 轮盘位置</summary>
        public Dictionary<int, int> BackpackToWheelMapping;

        /// <summary>记录每个轮盘位置的物品来源（背包 vs 插槽）</summary>
        public bool[] IsFromSlot;

        /// <summary>
        /// 手雷堆叠信息映射（保留兼容）
        /// 新架构中应使用 DisplayedItems 中的 CollectedItemInfo
        /// </summary>
        public Dictionary<int, CollectedItemInfo> ItemInfoMap;

        /// <summary>
        /// 构造函数
        /// </summary>
        public CategoryWheel()
        {
            // 初始化显示槽位
            Slots = new Item[9];  // 9宫格
            LastConfirmedIndex = -1;
            IsFirstLoad = true;

            // 初始化数据列表
            AllItems = new List<CollectedItemInfo>();
            DisplayedItems = new List<CollectedItemInfo>();

            // 初始化映射数据结构（8个轮盘位置）
            WheelToBackpackMapping = new int[8];
            System.Array.Fill(WheelToBackpackMapping, -1); // -1 表示空位
            BackpackToWheelMapping = new Dictionary<int, int>();
            IsFromSlot = new bool[8];  // 默认全为false（来自背包）
            ItemInfoMap = new Dictionary<int, CollectedItemInfo>();
        }
    }
}
