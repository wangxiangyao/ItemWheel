using System;
using Duckov.Modding;
using HarmonyLib;
using UnityEngine;

namespace ItemWheel.Integration.Compatibility
{
    /// <summary>
    /// Detects the Shoulder Surfing (third-person) mod so mouse/aim overrides are only enabled when needed.
    /// </summary>
    internal static class ShoulderSurfingCompatibility
    {
        private const string ModName = "Shoulder Surfing";

        private static bool _eventRegistered;

        public static bool IsActive { get; private set; }

        public static void TryInitialize()
        {
            if (IsActive)
            {
                return;
            }

            if (IsShoulderSurfingAssemblyLoaded())
            {
                Activate();
                return;
            }

            RegisterModActivatedEvent();
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
            if (!MatchesShoulderSurfing(modInfo, modBehaviour))
            {
                return;
            }

            Activate();
        }

        private static bool MatchesShoulderSurfing(ModInfo modInfo, Duckov.Modding.ModBehaviour modBehaviour)
        {
            if (modBehaviour != null)
            {
                string fullName = modBehaviour.GetType()?.FullName;
                if (!string.IsNullOrEmpty(fullName) &&
                    fullName.IndexOf("ShoulderSurfing", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(modInfo.name) &&
                modInfo.name.IndexOf("ShoulderSurfing", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(modInfo.displayName) &&
                modInfo.displayName.IndexOf("ShoulderSurfing", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        private static bool IsShoulderSurfingAssemblyLoaded()
        {
            return AccessTools.TypeByName("ShoulderSurfing.ShoulderCamera") != null;
        }

        private static void Activate()
        {
            if (IsActive)
            {
                return;
            }

            IsActive = true;
            Debug.Log("[ItemWheel] Shoulder Surfing detected, enabling cursor/aim integration.");
            UnregisterModActivatedEvent();
        }
    }
}
