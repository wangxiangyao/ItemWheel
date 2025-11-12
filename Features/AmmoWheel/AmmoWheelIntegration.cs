using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using ItemWheel.Integration;

namespace ItemWheel
{
    /// <summary>
    /// 将 AmmoWheelSystem 挂接到游戏输入（R 键）与每帧更新。
    /// 不修改 ModBehaviour.cs，避免编码问题；通过 HarmonyPatch 集成。
    /// </summary>
    [HarmonyPatch]
    internal static class AmmoWheelIntegration
    {
        private static readonly AmmoWheelSystem _ammo = new AmmoWheelSystem();

        // 每帧更新：挂在 CharacterInputControl.Update 之后
        [HarmonyPatch(typeof(CharacterInputControl), "Update")]
        [HarmonyPostfix]
        private static void CharacterInput_Update_Postfix()
        {
            _ammo.Update();
        }

        // R 键：短按原生换弹，长按呼出子弹轮盘
        [HarmonyPatch(typeof(CharacterInputControl), "OnReloadInput")]
        [HarmonyPrefix]
        private static bool OnReloadInput_Prefix(InputAction.CallbackContext context)
        {
            if (!ModSettingFacade.Settings.EnableAmmoWheel)
            {
                return true;
            }

            try
            {
                if (context.started || (context.performed && !context.canceled))
                {
                    _ammo.OnKeyPressed();
                }

                if (context.canceled)
                {
                    _ammo.OnKeyReleased();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AmmoWheel] 处理换弹输入失败: {ex}");
            }

            // 由我们统一处理短/长按，阻止原方法
            return false;
        }

        // 当子弹轮盘显示时，清除鼠标触发标志，避免误开火
        [HarmonyPatch(typeof(CharacterInputControl), "OnPlayerTriggerInputUsingMouseKeyboard")]
        [HarmonyPostfix]
        private static void OnPlayerTriggerInput_Postfix(CharacterInputControl __instance)
        {
            if (!_ammo.HasActiveWheel)
            {
                return;
            }

            try
            {
                var type = typeof(CharacterInputControl);
                var field1 = type.GetField("mouseKeyboardTriggerInputThisFrame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var field2 = type.GetField("mouseKeyboardTriggerInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var field3 = type.GetField("mouseKeyboardTriggerReleaseThisFrame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field1?.SetValue(__instance, false);
                field2?.SetValue(__instance, false);
                field3?.SetValue(__instance, false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AmmoWheel] Failed to clear trigger flags: {ex}");
            }
        }

        // 当子弹轮盘显示时，阻止鼠标滚轮（防止切换武器/交互）
        [HarmonyPatch(typeof(CharacterInputControl), "OnMouseScollerInput")]
        [HarmonyPrefix]
        private static bool OnMouseScrollerInput_Prefix()
        {
            if (_ammo.HasActiveWheel)
            {
                return false;
            }
            return true;
        }
    }
}
