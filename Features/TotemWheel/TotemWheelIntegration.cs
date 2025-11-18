using System;
using HarmonyLib;
using UnityEngine;

namespace ItemWheel.Features.TotemWheel
{
    [HarmonyPatch]
    internal static class TotemWheelIntegration
    {
        private static readonly TotemWheelSystem TotemWheel = new TotemWheelSystem();
        private static readonly System.Reflection.BindingFlags PrivateInstance = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        [HarmonyPatch(typeof(CharacterInputControl), "Update")]
        [HarmonyPostfix]
        private static void CharacterInput_Update_Postfix()
        {
            TotemWheel.Update();
        }

        [HarmonyPatch(typeof(CharacterInputControl), "OnPlayerTriggerInputUsingMouseKeyboard")]
        [HarmonyPostfix]
        private static void OnPlayerTriggerInput_Postfix(CharacterInputControl __instance)
        {
            if (!TotemWheel.HasActiveWheel)
            {
                return;
            }

            try
            {
                var type = typeof(CharacterInputControl);
                type.GetField("mouseKeyboardTriggerInputThisFrame", PrivateInstance)?.SetValue(__instance, false);
                type.GetField("mouseKeyboardTriggerInput", PrivateInstance)?.SetValue(__instance, false);
                type.GetField("mouseKeyboardTriggerReleaseThisFrame", PrivateInstance)?.SetValue(__instance, false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TotemWheel] Failed to clear trigger flags: {ex}");
            }
        }

        [HarmonyPatch(typeof(CharacterInputControl), "OnMouseScollerInput")]
        [HarmonyPrefix]
        private static bool OnMouseScrollerInput_Prefix()
        {
            if (TotemWheel.HasActiveWheel)
            {
                return false;
            }

            return true;
        }
    }
}
