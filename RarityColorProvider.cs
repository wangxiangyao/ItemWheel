using UnityEngine;
using ItemStatsSystem;

namespace ItemWheel
{
    public static class RarityColorProvider
    {
        // 文本颜色（不透明），用于名称着色
        public static readonly Color White;
        public static readonly Color Green;
        public static readonly Color Blue;
        public static readonly Color Purple;
        public static readonly Color Orange;
        public static readonly Color LightRed;
        public static readonly Color Red;
        public static readonly Color Gold; // 最高级（Q8）使用金色

        static RarityColorProvider()
        {
            ColorUtility.TryParseHtmlString("#FFFFFF", out White);
            ColorUtility.TryParseHtmlString("#7CFF7C", out Green);
            ColorUtility.TryParseHtmlString("#7CD5FF", out Blue);
            ColorUtility.TryParseHtmlString("#D0ACFF", out Purple);
            ColorUtility.TryParseHtmlString("#FFDC24", out Orange);
            ColorUtility.TryParseHtmlString("#FF5858", out LightRed);
            ColorUtility.TryParseHtmlString("#BB0000", out Red);
            ColorUtility.TryParseHtmlString("#FFD700", out Gold);
        }

        // 兼容旧接口：少量地方还按int质量使用
        public static Color GetTintByQuality(int quality)
        {
            if (quality <= 0) return White;
            return quality switch
            {
                1 => Green,
                2 => Blue,
                3 => Purple,
                4 => Orange,
                5 => LightRed,
                _ => Red
            };
        }

        public static Color GetTextColorByDisplayQuality(DisplayQuality q)
        {
            // 颜色整体“再降一级”映射：
            // Green→White, Blue→Green, Purple→Blue, Orange→Purple, Red→Orange, Q7→Orange, Q8→Gold
            return q switch
            {
                DisplayQuality.White => White,
                DisplayQuality.Green => White,
                DisplayQuality.Blue => Green,
                DisplayQuality.Purple => Blue,
                DisplayQuality.Orange => Purple,
                DisplayQuality.Red => Orange,
                DisplayQuality.Q7 => Orange,
                DisplayQuality.Q8 => Gold,
                _ => White
            };
        }

        // 再降一级的质量→文本颜色（整数质量）：在上一版基础上整体再降一级
        public static Color GetTextColorByQualityDegraded(int quality)
        {
            // 假定原始分布：0白,1绿,2蓝,3紫,4橙,5红,6+更高
            // 现在“再降一级”：
            // 0/1/2 → 白, 3 → 绿, 4 → 蓝, 5 → 紫, 6 → 橙, 7+ → 金
            if (quality <= 2) return White;   // 0/1/2 → 白
            if (quality == 3) return Green;   // 3 → 绿
            if (quality == 4) return Blue;    // 4 → 蓝
            if (quality == 5) return Purple;  // 5 → 紫
            if (quality == 6) return Orange;  // 6 → 橙
            return Gold;                      // 7+ → 金（最高级）
        }
    }
}
