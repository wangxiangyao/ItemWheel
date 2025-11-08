using UnityEngine;

namespace ItemWheel
{
    public static class RarityColorProvider
    {
        public static readonly Color White;
        public static readonly Color Green;
        public static readonly Color Blue;
        public static readonly Color Purple;
        public static readonly Color Orange;
        public static readonly Color LightRed;
        public static readonly Color Red;

        static RarityColorProvider()
        {
            ColorUtility.TryParseHtmlString("#FFFFFF00", out White);
            ColorUtility.TryParseHtmlString("#7cff7c40", out Green);
            ColorUtility.TryParseHtmlString("#7cd5ff40", out Blue);
            ColorUtility.TryParseHtmlString("#d0acff40", out Purple);
            ColorUtility.TryParseHtmlString("#ffdc2496", out Orange);
            ColorUtility.TryParseHtmlString("#ff585896", out LightRed);
            ColorUtility.TryParseHtmlString("#bb000096", out Red);
        }

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
    }
}

