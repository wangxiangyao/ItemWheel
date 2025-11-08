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
            // 子弹不降一级：按原始整数 Quality 映射颜色
            var tint = RarityColorProvider.GetTintByQuality(item.Quality);
            return new WheelItemWithDecor(item, overrideRightText: right, overrideTint: tint, rightAlign: true);
        }

        public Item FromWheelItem(IWheelItem item)
        {
            return null;
        }
    }
}
