using UnityEngine;

namespace ItemWheel.Integration.Compatibility
{
    internal static class CompatibilityBootstrap
    {
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            Debug.Log("[ItemWheel] Initializing compatibility modules...");

            CashSlotCompatibility.TryInitialize();
        }
    }
}
