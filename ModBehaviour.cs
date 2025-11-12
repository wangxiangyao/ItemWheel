using System;
using System.Collections.Generic;
using HarmonyLib;
using Duckov.Modding;
using UnityEngine;
using UnityEngine.InputSystem;
using ItemWheel.Integration;

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

            // 创建轮盘系统
            _wheelSystem = new ItemWheelSystem();

            _harmony = new Harmony("com.duckov.itemwheel");
            _harmony.PatchAll(typeof(ModBehaviour).Assembly);
        }

        /// <summary>
        /// 游戏和ModManager初始化完成后调用
        /// 这是初始化 ModSetting 的正确时机
        /// </summary>
        protected override void OnAfterSetup()
        {
            // 🆕 在 OnAfterSetup 中初始化 ModSettingFacade
            // 因为此时 ModSetting 才准备好
            try
            {
                ModSettingFacade.Initialize(this.info);
                Debug.Log($"[ItemWheel] ModSetting available: {ModSettingFacade.IsModSettingAvailable}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemWheel] Failed to initialize ModSettingFacade: {ex.Message}");
            }
        }

        private void Update()
        {
            _wheelSystem?.Update(); // ✅ 步骤2恢复：更新轮盘系统
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _harmony?.UnpatchAll(_harmony.Id);
                _wheelSystem?.Dispose(); // ✅ 步骤2恢复：释放轮盘系统
                _instance = null;
            }
        }

        // ✅ 步骤2恢复：Harmony输入补丁
        [HarmonyPatch(typeof(CharacterInputControl))]
        private static class CharacterInputPatch
        {
            [HarmonyPatch("OnShortCutInput3")]
            [HarmonyPrefix]
            private static bool OnShortCutInput3(InputAction.CallbackContext context) => Forward(context, 0);

            [HarmonyPatch("OnShortCutInput4")]
            [HarmonyPrefix]
            private static bool OnShortCutInput4(InputAction.CallbackContext context) => Forward(context, 1);

            [HarmonyPatch("OnShortCutInput5")]
            [HarmonyPrefix]
            private static bool OnShortCutInput5(InputAction.CallbackContext context) => Forward(context, 2);

            [HarmonyPatch("OnShortCutInput6")]
            [HarmonyPrefix]
            private static bool OnShortCutInput6(InputAction.CallbackContext context) => Forward(context, 3);

            [HarmonyPatch("OnPlayerSwitchItemAgentMelee")]
            [HarmonyPrefix]
            private static bool OnPlayerSwitchItemAgentMelee_Prefix(InputAction.CallbackContext context)
            {
                if (_instance == null) return true;

                try
                {
                    // started: 开始计时（不拦截官方方法）
                    if (context.started)
                    {
                        _instance._wheelSystem.OnKeyPressed(ItemWheelSystem.ItemWheelCategory.Melee);
                        return true; // 允许官方方法继续
                    }

                    // canceled: 按键松开
                    if (context.canceled)
                    {
                        // 检查是否触发了轮盘（长按）
                        bool hasTriggeredWheel = _instance._wheelSystem.HasTriggeredWheel(ItemWheelSystem.ItemWheelCategory.Melee);

                        _instance._wheelSystem.OnKeyReleased(ItemWheelSystem.ItemWheelCategory.Melee);

                        // 如果触发了轮盘（长按），拦截官方方法的 canceled 处理
                        // 如果没触发轮盘（短按），允许官方方法处理
                        return !hasTriggeredWheel; // 长按返回false拦截，短按返回true放行
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ItemWheel] 处理近战快捷键失败: {ex}");
                    return true;
                }

                // 其他事件（如performed）：不拦截，让官方方法正常执行
                return true;
            }

            private static bool Forward(InputAction.CallbackContext context, int shortcutIndex)
            {
                if (_instance == null) return true;

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
                if (_instance?._wheelSystem?.HasActiveWheel == true)
                {
                    try
                    {
                        var type = typeof(CharacterInputControl);
                        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                        type.GetField("mouseKeyboardTriggerInputThisFrame", flags)?.SetValue(__instance, false);
                        type.GetField("mouseKeyboardTriggerInput", flags)?.SetValue(__instance, false);
                        type.GetField("mouseKeyboardTriggerReleaseThisFrame", flags)?.SetValue(__instance, false);
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
                if (_instance?._wheelSystem?.HasActiveWheel == true)
                    return false;  // 轮盘显示时阻止滚轮输入

                return true;
            }
        }

        private bool HandleShortcutContext(int shortcutIndex, InputAction.CallbackContext context)
        {
            var category = GetItemCategoryForShortcut(shortcutIndex);

            if (context.started || (context.performed && !context.canceled))
                _wheelSystem.OnKeyPressed(category);

            if (context.canceled)
                _wheelSystem.OnKeyReleased(category);

            return false;
        }

        private static ItemWheelSystem.ItemWheelCategory GetItemCategoryForShortcut(int shortcutIndex) => shortcutIndex switch
        {
            0 => ItemWheelSystem.ItemWheelCategory.Medical,
            1 => ItemWheelSystem.ItemWheelCategory.Stim,
            2 => ItemWheelSystem.ItemWheelCategory.Food,
            3 => ItemWheelSystem.ItemWheelCategory.Explosive,
            _ => ItemWheelSystem.ItemWheelCategory.Medical
        };
    }
}

