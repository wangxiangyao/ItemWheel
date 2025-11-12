using System;
using ItemStatsSystem;
using QuickWheel.Core.Interfaces;
using UnityEngine;
using ItemWheel.Data;

namespace ItemWheel
{
    using ItemWheelCategory = ItemWheel.ItemWheelSystem.ItemWheelCategory; // 🆕 显式导入 ItemWheelCategory 类型
    /// <summary>
    /// 上下文感知的物品适配器，能够访问轮盘信息以获取堆叠数量等自定义数据
    /// </summary>
    internal class ItemWheelContextualAdapter : IWheelItemAdapter<Item>
    {
        private readonly CategoryWheel _wheel;

        public ItemWheelContextualAdapter(CategoryWheel wheel)
        {
            _wheel = wheel;
        }

        public IWheelItem ToWheelItem(Item item)
        {
            if (item == null)
            {
                return null;
            }

            // 🆕 手雷特殊处理：查找堆叠信息
            int? stackCount = null;
            if (_wheel?.Category == ItemWheelCategory.Explosive &&
                _wheel.ItemInfoMap != null)
            {
                // 遍历 ItemInfoMap 查找匹配的 Item（因为 Item 引用可能不匹配）
                foreach (var kvp in _wheel.ItemInfoMap)
                {
                    int backpackIndex = kvp.Key;
                    CollectedItemInfo info = kvp.Value;

                    // 比较 TypeID 和 DisplayName（因为 Item 对象引用可能不同）
                    if (info.Item != null &&
                        info.Item.TypeID == item.TypeID &&
                        info.Item.DisplayName == item.DisplayName)
                    {
                        if (info.StackCount > 1)
                        {
                            stackCount = info.StackCount;
                        }
                        break;
                    }
                }
            }

            return new WheelItemWithDecor(item, overrideStackCount: stackCount);
        }

        public Item FromWheelItem(IWheelItem item)
        {
            return null;
        }
    }
}
