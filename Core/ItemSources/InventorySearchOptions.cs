using System;
using System.Collections.Generic;
using System.Linq;
using ItemStatsSystem;
using ItemWheel.Integration;

namespace ItemWheel.Core.ItemSources
{
    /// <summary>
    /// 描述一次物品搜索的上下文信息。
    /// </summary>
    public sealed class InventorySearchOptions
    {
        public InventorySearchOptions(
            IEnumerable<Inventory> inventories,
            Func<Item, bool> matchPredicate,
            ItemWheelModSettings settings,
            CharacterMainControl character = null)
        {
            Inventories = (inventories ?? Array.Empty<Inventory>()).Where(inv => inv != null).ToList();
            MatchPredicate = matchPredicate ?? throw new ArgumentNullException(nameof(matchPredicate));
            Settings = settings;
            IncludeContainerSlots = settings?.SearchInSlots ?? true;
            Character = character;
        }

        /// <summary>候选背包列表（主背包、宠物背包等）。</summary>
        public IReadOnlyList<Inventory> Inventories { get; }

        /// <summary>外部传入的匹配条件。</summary>
        public Func<Item, bool> MatchPredicate { get; }

        /// <summary>轮盘的 mod 设置（可选，用于扩展数据源读取更多偏好）。</summary>
        public ItemWheelModSettings Settings { get; }

        /// <summary>是否需要搜索容器插槽。</summary>
        public bool IncludeContainerSlots { get; }

        /// <summary>当前玩家角色（某些扩展数据源可能需要）。</summary>
        public CharacterMainControl Character { get; }
    }
}
