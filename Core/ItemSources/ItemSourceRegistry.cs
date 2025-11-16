using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ItemWheel.Core.ItemSources
{
    /// <summary>
    /// 统一管理所有可用的物品数据源。
    /// </summary>
    public static class ItemSourceRegistry
    {
        private static readonly List<IItemSource> SourcesInternal = new List<IItemSource>();
        private static readonly object SyncRoot = new object();

        static ItemSourceRegistry()
        {
            Register(new Sources.InventoryItemSource());
        }

        public static IReadOnlyList<IItemSource> Sources
        {
            get
            {
                lock (SyncRoot)
                {
                    return SourcesInternal.ToList();
                }
            }
        }

        public static void Register(IItemSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            lock (SyncRoot)
            {
                if (SourcesInternal.Any(existing => existing.GetType() == source.GetType()))
                {
                    Debug.Log($"[ItemWheel] ItemSource '{source.Name}' already registered, skipping duplicate.");
                    return;
                }
                SourcesInternal.Add(source);
                Debug.Log($"[ItemWheel] Registered item source: {source.Name}");
            }
        }
    }
}
