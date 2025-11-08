using System.Collections.Generic;
using ItemStatsSystem;
using QuickWheel.Core.Interfaces;
using UnityEngine;

namespace ItemWheel
{
    public class BulletWheelAdapter : IWheelItemAdapter<Item>
    {
        private readonly Dictionary<int, int> _typeCounts;

        public BulletWheelAdapter(Dictionary<int, int> typeCounts)
        {
            _typeCounts = typeCounts ?? new Dictionary<int, int>();
        }

        public IWheelItem ToWheelItem(Item item)
        {
            if (item == null) return null;
            string right = null;
            if (_typeCounts.TryGetValue(item.TypeID, out var count) && count > 0)
            {
                right = "x" + count.ToString();
            }
            // 子弹同样显示稀有度颜色；耐久一般无，按默认逻辑
            return new WheelItemWithDecor(item, overrideRightText: right, rightAlign: true);
        }

        public Item FromWheelItem(IWheelItem item)
        {
            return null;
        }
    }
}

