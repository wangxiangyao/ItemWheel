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

            private static bool Forward(InputAction.CallbackContext context, int shortcutIndex)
            {
                var instance = _instance;
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

            /// <summary>
            /// Patch 鼠标触发输入（左键点击/开火）
            /// 当轮盘显示时，清除触发标志，防止游戏开火
            /// </summary>
            [HarmonyPatch("OnPlayerTriggerInputUsingMouseKeyboard")]
            [HarmonyPostfix]
            private static void OnPlayerTriggerInputPostfix(CharacterInputControl __instance)
            {
                // 当轮盘显示时，清除所有触发标志
                if (_instance?._wheelSystem?.HasActiveWheel == true)
                {
                    try
                    {
                        // 使用反射清除私有字段
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

            /// <summary>
            /// Patch 鼠标滚轮输入
            /// 当轮盘显示时，阻止鼠标滚轮（防止切换武器）
            /// </summary>
            [HarmonyPatch("OnMouseScollerInput")]
            [HarmonyPrefix]
            private static bool OnMouseScrollerInputPrefix(InputAction.CallbackContext context)
            {
                // 当轮盘显示时，阻止鼠标滚轮输入
                if (_instance?._wheelSystem?.HasActiveWheel == true)
                {
                    return false;  // 阻止原方法执行
                }

                return true;  // 允许原方法执行
            }
        }

        /// <summary>
        /// 处理快捷键输入
        /// 拦截官方快捷键，转发给轮盘系统处理
        /// </summary>
        private bool HandleShortcutContext(int shortcutIndex, InputAction.CallbackContext context)
        {
            // 根据快捷键索引获取对应的物品类别
            var category = GetItemCategoryForShortcut(shortcutIndex);

            if (context.started || (context.performed && !context.canceled))
            {
                // 按键按下：通知轮盘系统开始监听长按
                _wheelSystem.OnKeyPressed(category);
            }

            if (context.canceled)
            {
                // 按键松开：通知轮盘系统结束监听
                _wheelSystem.OnKeyReleased(category);
            }

            // 阻止游戏默认的快捷键行为
            return false;
        }

        /// <summary>
        /// 根据快捷键索引获取物品类别
        /// 这对应游戏的快捷键设置：3=医疗, 4=刺激剂, 5=食物, 6=爆炸物
        /// </summary>
        private static ItemWheelSystem.ItemWheelCategory GetItemCategoryForShortcut(int shortcutIndex)
        {
            return shortcutIndex switch
            {
                0 => ItemWheelSystem.ItemWheelCategory.Medical,   // 官方快捷键3
                1 => ItemWheelSystem.ItemWheelCategory.Stim,      // 官方快捷键4
                2 => ItemWheelSystem.ItemWheelCategory.Food,      // 官方快捷键5
                3 => ItemWheelSystem.ItemWheelCategory.Explosive, // 官方快捷键6
                _ => ItemWheelSystem.ItemWheelCategory.Medical
            };
        }

      
    }
}
