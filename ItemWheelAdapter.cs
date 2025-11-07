using ItemStatsSystem;
using QuickWheel.Core.Interfaces;
using QuickWheel.Utils;

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

            return new WheelItemWrapper
            {
                Icon = item.Icon,
                DisplayName = item.DisplayName,
                IsValid = !string.IsNullOrEmpty(item.DisplayName)
            };
        }

        public Item FromWheelItem(IWheelItem item)
        {
            return null;
        }
    }
}
