using System;
using System.Collections.Generic;
using HarmonyLib;
using Duckov.Modding;
using UnityEngine;
using UnityEngine.InputSystem;
using ItemWheel.Integration;
using ItemWheel.Utils;

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

                // 🆕 配置初始化完成后，重新初始化子弹时间
                // 因为 ItemWheelSystem 的构造函数在 Awake() 中执行，那时配置还未加载
                _wheelSystem?.ReinitializeBulletTime();
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

        // ✅ Hook ItemShortcut.Load()，在快捷栏从存档加载完成后初始化轮盘
        [HarmonyPatch(typeof(Duckov.ItemShortcut), "Load")]
        private static class ItemShortcutLoadPatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                Debug.Log("[ItemWheel] ✅ ItemShortcut.Load() 完成，初始化轮盘...");

                // 等快捷栏加载完成后，初始化轮盘
                if (_instance?._wheelSystem != null)
                {
                    _instance._wheelSystem.InitializeAllWheelsOnStart();
                }
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
                // 🆕 检测UI输入框焦点，避免与UI输入冲突
                if (UIFocusDetector.IsInputFieldFocused())
                {
                    return true; // 有输入框获得焦点，放行官方输入
                }

                if (_instance == null) return true;

                bool isEnabled = ModSettingFacade.Settings.IsWheelEnabled(ItemWheelSystem.ItemWheelCategory.Melee);
                Debug.Log($"[ItemWheel] 近战快捷键触发, 轮盘启用状态: {isEnabled}, context: {context.phase}");

                if (!isEnabled)
                {
                    Debug.Log($"[ItemWheel] 近战轮盘已禁用，允许原始输入");
                    return true;
                }

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
                // 🆕 检测UI输入框焦点，避免与UI输入冲突
                if (UIFocusDetector.IsInputFieldFocused())
                {
                    return true; // 有输入框获得焦点，放行官方输入
                }

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
                {
                    // 轮盘显示时，拦截游戏滚轮输入，但转发给轮盘系统处理
                    if (context.performed)
                    {
                        Vector2 scrollValue = context.ReadValue<Vector2>();
                        float scrollY = scrollValue.y;

                        Debug.Log($"[ItemWheel] 滚轮输入拦截: scrollY={scrollY}");

                        if (Mathf.Abs(scrollY) > 0.01f)
                        {
                            int direction = scrollY > 0 ? -1 : 1;
                            _instance._wheelSystem.OnWheelScroll(direction);
                        }
                    }
                    return false;
                }

                return true;
            }
        }

        private bool HandleShortcutContext(int shortcutIndex, InputAction.CallbackContext context)
        {
            var category = GetItemCategoryForShortcut(shortcutIndex);
            bool isEnabled = ModSettingFacade.Settings.IsWheelEnabled(category);

            Debug.Log($"[ItemWheel] 快捷键 {shortcutIndex} ({category}) 触发, 轮盘启用状态: {isEnabled}, context: {context.phase}");

            if (!isEnabled)
            {
                Debug.Log($"[ItemWheel] 轮盘 {category} 已禁用，允许原始输入");
                return true;
            }

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
