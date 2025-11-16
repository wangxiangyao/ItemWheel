using System;
using System.Reflection;
using ItemWheel.Core.ItemSources;
using ItemWheel.Core.ItemSources.Sources;
using UnityEngine;

namespace ItemWheel.Integration.Compatibility
{
    internal static class CashSlotCompatibility
    {
        private static bool _pending;
        private static bool _registered;

        public static void TryInitialize()
        {
            if (_registered)
            {
                return;
            }

            if (CashSlotItemSource.IsSupported)
            {
                Register();
                return;
            }

            if (_pending)
            {
                return;
            }

            _pending = true;
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoaded;
            Debug.Log("[ItemWheel] Waiting for Duckov_CashSlot assembly to load...");
        }

        private static void OnAssemblyLoaded(object sender, AssemblyLoadEventArgs e)
        {
            if (_registered)
            {
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoaded;
                return;
            }

            if (!CashSlotItemSource.IsSupported)
            {
                return;
            }

            Register();
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoaded;
        }

        private static void Register()
        {
            if (_registered)
            {
                return;
            }
            ItemSourceRegistry.Register(new CashSlotItemSource());
            _registered = true;
            Debug.Log("[ItemWheel] Enabled compatibility for Duckov_CashSlot slots.");
        }
    }
}
