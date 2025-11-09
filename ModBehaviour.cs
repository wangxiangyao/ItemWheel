using System;
using HarmonyLib;
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
                    if (context.started || (context.performed && !context.canceled))
                        _instance._wheelSystem.OnKeyPressed(ItemWheelSystem.ItemWheelCategory.Melee);

                    if (context.canceled)
                        _instance._wheelSystem.OnKeyReleased(ItemWheelSystem.ItemWheelCategory.Melee);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ItemWheel] 处理近战快捷键失败: {ex}");
                    return true;
                }

                return false; // 阻止原方法，避免与官方近战切换冲突
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

