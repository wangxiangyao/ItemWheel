using UnityEngine;

namespace ItemWheel.Integration.Compatibility
{
    /// <summary>
    /// 兼容性模块初始化入口
    /// </summary>
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

            // ✅ 使用官方 ModManager.OnModActivated 事件（安全，不干扰其他 mod）
            CashSlotCompatibility.TryInitialize();
        }
    }
}
