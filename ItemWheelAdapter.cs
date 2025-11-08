using ItemStatsSystem;
using QuickWheel.Core.Interfaces;
using QuickWheel.Utils;
using UnityEngine;

namespace ItemWheel
{
    /// <summary>
    /// 将游戏中的物品适配为 QuickWheel 可显示的数据结构。
    /// </summary>
    public class ItemWheelAdapter : IWheelItemAdapter<Item>
    {
        public IWheelItem ToWheelItem(Item item)
        {
            if (item == null)
            {
                return null;
            }

            // 返回带装饰的包装，支持稀有度/堆叠数量/耐久
            return new WheelItemWithDecor(item);
        }

        public Item FromWheelItem(IWheelItem item)
        {
            return null;
        }
    }
}
