using System;
using ItemWheel.Core.ItemSources;
using ItemWheel.Core.ItemSources.Sources;
using Duckov.Modding;
using UnityEngine;

namespace ItemWheel.Integration.Compatibility
{
    /// <summary>
    /// CashSlot 兼容性管理 - 使用官方 ModManager 事件，安全监听 mod 激活
    /// </summary>
    internal static class CashSlotCompatibility
    {
        private static bool _registered;
        private static bool _eventRegistered;

        public static void TryInitialize()
        {
            if (_registered)
            {
                return;
            }

            // 1. 立即检测 CashSlot 是否已加载
            if (CashSlotItemSource.IsSupported)
            {
                Register();
                return;
            }

            // 2. 如果未加载，注册官方 mod 激活事件等待
            if (!_eventRegistered)
            {
                RegisterModActivatedEvent();
                Debug.Log("[ItemWheel] Waiting for Duckov_CashSlot mod to activate...");
            }
        }

        private static void RegisterModActivatedEvent()
        {
            if (_eventRegistered)
            {
                return;
            }

            ModManager.OnModActivated += OnModActivated;
            _eventRegistered = true;
        }

        private static void UnregisterModActivatedEvent()
        {
            if (!_eventRegistered)
            {
                return;
            }

            ModManager.OnModActivated -= OnModActivated;
            _eventRegistered = false;
        }

        private static void OnModActivated(ModInfo modInfo, Duckov.Modding.ModBehaviour modBehaviour)
        {
            // 忽略 ItemWheel 自己的激活事件
            if (modBehaviour != null && modBehaviour.GetType().Assembly == typeof(CashSlotCompatibility).Assembly)
            {
                return;
            }

            // ✅ 先用 name/ID 快速过滤，避免不必要的反射检测
            const string CASHSLOT_NAME = "Duckov_CashSlot";
            const ulong CASHSLOT_PUBLISHED_ID = 3595993103; // CashSlot 的 Workshop ID

            bool isCashSlot = modInfo.name == CASHSLOT_NAME ||
                             modInfo.publishedFileId == CASHSLOT_PUBLISHED_ID;

            if (!isCashSlot)
            {
                return; // 不是 CashSlot，直接跳过
            }

            Debug.Log($"[ItemWheel] CashSlot mod detected ({modInfo.name}), checking compatibility...");

            // 确认反射绑定成功
            if (CashSlotItemSource.IsSupported)
            {
                Register();
                UnregisterModActivatedEvent();
            }
            else
            {
                Debug.LogWarning("[ItemWheel] CashSlot mod activated but reflection binding failed.");
            }
        }

        private static void Register()
        {
            if (_registered)
            {
                return;
            }

            ItemSourceRegistry.Register(new CashSlotItemSource());
            _registered = true;
            Debug.Log("[ItemWheel] ✅ Enabled compatibility for Duckov_CashSlot mod.");
        }
    }
}
