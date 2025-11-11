using ItemStatsSystem;
using QuickWheel.Core.Interfaces;
using QuickWheel.Utils;
using UnityEngine;

namespace ItemWheel
{
    public class WheelItemWithDecor : IWheelItem, IWheelItemDecor
    {
        private readonly Item _item;
        private readonly string _overrideRightText;
        private readonly Color? _overrideTint;
        private readonly float? _overrideDurability01;
        private readonly bool _rightAlign;
        private readonly int? _overrideStackCount; // 🆕 手雷堆叠数量覆盖

        public WheelItemWithDecor(Item item, string overrideRightText = null, Color? overrideTint = null, float? overrideDurability01 = null, bool rightAlign = true, int? overrideStackCount = null)
        {
            _item = item;
            _overrideRightText = overrideRightText;
            _overrideTint = overrideTint;
            _overrideDurability01 = overrideDurability01;
            _rightAlign = rightAlign;
            _overrideStackCount = overrideStackCount; // 🆕 初始化
        }

        public Sprite GetIcon() => _item?.Icon;
        public string GetDisplayName()
        {
            if (_item == null) return null;
            string rawName = _item.DisplayName ?? string.Empty;

            // 名称上色（仅文字）
            var tint = GetRarityTint();
            string nameColored = rawName;
            if (tint.HasValue)
            {
                var c = tint.Value;
                string hex = ColorUtility.ToHtmlStringRGB(new Color(c.r, c.g, c.b, 1f));
                nameColored = $"<color=#{hex}>{rawName}</color>";
            }

            // 第一行：数量或耐久百分比
            string topLine = null;
            var d01 = GetDurability01();
            if (d01.HasValue && d01.Value > 0f)
            {
                int pct = Mathf.Clamp(Mathf.RoundToInt(d01.Value * 100f), 0, 100);
                topLine = $"{pct}%";
            }
            else
            {
                if (!string.IsNullOrEmpty(_overrideRightText))
                {
                    topLine = _overrideRightText;
                }
                // 🆕 手雷堆叠特殊处理：优先使用覆盖的堆叠数量
                else if (_overrideStackCount.HasValue && _overrideStackCount.Value > 1)
                {
                    topLine = $"x{_overrideStackCount.Value}";
                }
                else if (_item.Stackable && _item.StackCount > 1)
                {
                    topLine = $"x{_item.StackCount}";
                }
            }

            if (!string.IsNullOrEmpty(topLine))
            {
                return topLine + "\n" + nameColored; // 上：数量/百分比；下：名称
            }
            return nameColored;
        }
        public bool IsValid() => _item != null;

        public Color? GetRarityTint()
        {
            if (_overrideTint.HasValue) return _overrideTint;
            if (_item == null) return null;
            // 先按整数 Quality 再降一级映射
            var color = RarityColorProvider.GetTextColorByQualityDegraded(_item.Quality);
            // 若仍为白色，则尝试按 DisplayQuality 再降一级映射，提升可见度（适配子弹等）
            if (ApproximatelyWhite(color))
            {
                color = RarityColorProvider.GetTextColorByDisplayQuality(_item.DisplayQuality);
            }
            return color;
        }

        private static bool ApproximatelyWhite(Color c)
        {
            return c.a > 0.99f && c.r > 0.98f && c.g > 0.98f && c.b > 0.98f;
        }

        public string GetRightText()
        {
            if (!string.IsNullOrEmpty(_overrideRightText)) return _overrideRightText;
            if (_item == null) return null;

            // 🆕 手雷堆叠特殊处理：优先使用覆盖的堆叠数量
            if (_overrideStackCount.HasValue && _overrideStackCount.Value > 1)
            {
                return "x" + _overrideStackCount.Value.ToString();
            }

            if (_item.Stackable && _item.StackCount > 1)
            {
                return "x" + _item.StackCount.ToString();
            }
            return null;
        }

        public float? GetDurability01()
        {
            if (_overrideDurability01.HasValue) return _overrideDurability01;
            if (_item == null) return null;
            if (_item.UseDurability && _item.MaxDurability > 0f)
            {
                return Mathf.Clamp01(_item.Durability / Mathf.Max(0.0001f, _item.MaxDurability));
            }
            return null;
        }

        public bool RightAlign() => _rightAlign;
    }
}
