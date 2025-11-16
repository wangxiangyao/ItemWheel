using System.Collections.Generic;

namespace ItemWheel.Core.ItemSources
{
    /// <summary>
    /// 物品数据源：实现者负责根据上下文搜索可用物品。
    /// </summary>
    public interface IItemSource
    {
        /// <summary>用于调试的名称。</summary>
        string Name { get; }

        /// <summary>
        /// 收集匹配条件下的物品。
        /// </summary>
        IEnumerable<ItemWheel.SearchResult> CollectItems(InventorySearchOptions options);
    }
}
