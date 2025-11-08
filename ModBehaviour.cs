using System;
using Duckov;
using HarmonyLib;
using ItemStatsSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ItemWheel
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private static ModBehaviour _instance;

        private Harmony _harmony;
        private ItemWheelSystem _wheelSystem;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _wheelSystem = new ItemWheelSystem();

            _harmony = new Harmony("com.duckov.itemwheel");
            _harmony.PatchAll(typeof(ModBehaviour).Assembly);
        }

        private void Update()
        {
            // 每帧更新轮盘系统
            _wheelSystem?.Update();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _harmony?.UnpatchAll(_harmony.Id);
                _wheelSystem?.Dispose();
                _instance = null;
            }
        }

        [HarmonyPatch(typeof(CharacterInputControl))]
        private static class CharacterInputPatch
        {
            [HarmonyPatch("OnShortCutInput3")]
            [HarmonyPrefix]
            private static bool OnShortCutInput3(InputAction.CallbackContext context)
            {
                return Forward(context, 0);
            }

            [HarmonyPatch("OnShortCutInput4")]
            [HarmonyPrefix]
            private static bool OnShortCutInput4(InputAction.CallbackContext context)
            {
                return Forward(context, 1);
            }

            [HarmonyPatch("OnShortCutInput5")]
            [HarmonyPrefix]
            private static bool OnShortCutInput5(InputAction.CallbackContext context)
            {
                return Forward(context, 2);
            }

            [HarmonyPatch("OnShortCutInput6")]
            [HarmonyPrefix]
            private static bool OnShortCutInput6(InputAction.CallbackContext context)
            {
                return Forward(context, 3);
            }

            [HarmonyPatch("OnPlayerSwitchItemAgentMelee")]
            [HarmonyPrefix]
            private static bool OnPlayerSwitchItemAgentMelee_Prefix(InputAction.CallbackContext context)
            {
                if (_instance == null)
                {
                    return true;
                }

                try
                {
                    if (context.started || (context.performed && !context.canceled))
                    {
                        _instance._wheelSystem.OnKeyPressed(ItemWheelSystem.ItemWheelCategory.Melee);
                    }

                    if (context.canceled)
                    {
                        _instance._wheelSystem.OnKeyReleased(ItemWheelSystem.ItemWheelCategory.Melee);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ItemWheel] 处理近战快捷键失败: {ex}");
                    return true;
                }

                // 阻止原方法，避免与官方近战切换逻辑冲突
                return false;
            }

            private static bool Forward(InputAction.CallbackContext context, int shortcutIndex)
            {
                if (_instance == null)
                {
                    return true;
                }

                try
                {
                    return _instance.HandleShortcutContext(shortcutIndex, context);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ItemWheel] Failed to process shortcut index {shortcutIndex}: {ex}");
                    return true;
                }
            }

            [HarmonyPatch("OnPlayerTriggerInputUsingMouseKeyboard")]
            [HarmonyPostfix]
            private static void OnPlayerTriggerInputPostfix(CharacterInputControl __instance)
            {
                // 当轮盘显示时，清除所有触发标志
                if (_instance?._wheelSystem?.HasActiveWheel == true)
                {
                    try
                    {
                        var type = typeof(CharacterInputControl);

                        var field1 = type.GetField("mouseKeyboardTriggerInputThisFrame",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        field1?.SetValue(__instance, false);

                        var field2 = type.GetField("mouseKeyboardTriggerInput",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        field2?.SetValue(__instance, false);

                        var field3 = type.GetField("mouseKeyboardTriggerReleaseThisFrame",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        field3?.SetValue(__instance, false);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ItemWheel] Failed to clear trigger flags: {ex}");
                    }
                }
            }

            [HarmonyPatch("OnMouseScollerInput")]
            [HarmonyPrefix]
            private static bool OnMouseScrollerInputPrefix(InputAction.CallbackContext context)
            {
                // 当轮盘显示时，阻止鼠标滚轮输入（防止切换武器）
                if (_instance?._wheelSystem?.HasActiveWheel == true)
                {
                    return false;  // 阻止原方法执行
                }

                return true;  // 允许原方法执行
            }
        }

        private bool HandleShortcutContext(int shortcutIndex, InputAction.CallbackContext context)
        {
            var category = GetItemCategoryForShortcut(shortcutIndex);

            if (context.started || (context.performed && !context.canceled))
            {
                _wheelSystem.OnKeyPressed(category);
            }

            if (context.canceled)
            {
                _wheelSystem.OnKeyReleased(category);
            }

            return false;
        }

        private static ItemWheelSystem.ItemWheelCategory GetItemCategoryForShortcut(int shortcutIndex)
        {
            return shortcutIndex switch
            {
                0 => ItemWheelSystem.ItemWheelCategory.Medical,
                1 => ItemWheelSystem.ItemWheelCategory.Stim,
                2 => ItemWheelSystem.ItemWheelCategory.Food,
                3 => ItemWheelSystem.ItemWheelCategory.Explosive,
                _ => ItemWheelSystem.ItemWheelCategory.Medical
            };
        }
    }
}

