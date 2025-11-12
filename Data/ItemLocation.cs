using ItemStatsSystem;

namespace ItemWheel.Data
{
    /// <summary>
    /// 物品完整位置信息
    /// 记录物品在哪个背包、哪个索引、哪个slot
    /// </summary>
    public struct ItemLocation
    {
        /// <summary>所属背包（主背包或宠物背包）</summary>
        public Inventory Inventory;

        /// <summary>在该背包中的索引位置</summary>
        public int BackpackIndex;

        /// <summary>
        /// 如果在容器中，是这个索引位置物品的第几个slot
        /// -1 表示顶层物品（不在容器中）
        /// </summary>
        public int SlotIndex;

        /// <summary>是否来自slot（容器内）</summary>
        public bool IsFromSlot => SlotIndex >= 0;

        /// <summary>是否来自宠物背包</summary>
        public bool IsFromPet
        {
            get
            {
                try
                {
                    return Inventory != null && Inventory == PetProxy.PetInventory;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 是否可拖拽（只有主背包顶层物品可拖拽）
        /// </summary>
        public bool IsDraggable => !IsFromSlot && !IsFromPet;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="inventory">所属背包</param>
        /// <param name="backpackIndex">背包索引</param>
        /// <param name="slotIndex">slot索引（-1表示顶层物品）</param>
        public ItemLocation(Inventory inventory, int backpackIndex, int slotIndex = -1)
        {
            Inventory = inventory;
            BackpackIndex = backpackIndex;
            SlotIndex = slotIndex;
        }

        /// <summary>
        /// 用于调试的字符串表示
        /// </summary>
        public override string ToString()
        {
            string source = IsFromPet ? "宠物背包" : "主背包";
            string location = IsFromSlot ? $"[{BackpackIndex}]->slot[{SlotIndex}]" : $"[{BackpackIndex}]";
            return $"{source}{location}";
        }
    }
}
