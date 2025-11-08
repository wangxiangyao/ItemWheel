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

        public WheelItemWithDecor(Item item, string overrideRightText = null, Color? overrideTint = null, float? overrideDurability01 = null, bool rightAlign = true)
        {
            _item = item;
            _overrideRightText = overrideRightText;
            _overrideTint = overrideTint;
            _overrideDurability01 = overrideDurability01;
            _rightAlign = rightAlign;
        }

        public Sprite GetIcon() => _item?.Icon;
        public string GetDisplayName() => _item?.DisplayName;
        public bool IsValid() => _item != null;

        public Color? GetRarityTint()
        {
            if (_overrideTint.HasValue) return _overrideTint;
            if (_item == null) return null;
            // 使用整数 Quality 并整体降一级映射，保证与游戏现状一致
            return RarityColorProvider.GetTextColorByQualityDegraded(_item.Quality);
        }

        public string GetRightText()
        {
            if (!string.IsNullOrEmpty(_overrideRightText)) return _overrideRightText;
            if (_item == null) return null;
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
