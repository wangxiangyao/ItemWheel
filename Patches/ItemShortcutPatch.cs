using System;
using System.Linq;
using HarmonyLib;
using ItemStatsSystem;
using UnityEngine;

namespace ItemWheel.Patches
{
    /// <summary>
    /// 扩展官方快捷栏功能，支持容器和宠物背包中的物品
    /// 🆕 拦截Set方法，阻止不匹配类别的物品被设置到快捷栏
    /// </summary>
    [HarmonyPatch(typeof(Duckov.ItemShortcut))]
    internal static class ItemShortcutPatch
    {
        /// <summary>
        /// 🆕 拦截Set方法，检查物品类别是否匹配快捷栏
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("Set")]
        private static bool Set_Prefix(int index, Item item, ref bool __result)
        {
            try
            {
                // 如果是清空快捷栏（item==null），允许
                if (item == null)
                {
                    return true;
                }

                // 检查物品类别是否匹配快捷栏
                var category = ItemWheelSystem.GetCategoryForShortcutIndex(index);
                if (!ItemWheelSystem.IsItemMatchCategory(item, category))
                {
                    // 不匹配：显示提示并阻止设置
                    string categoryName = ItemWheelSystem.GetCategoryDisplayName(category);
                    string shortcutKey = (index + 3).ToString(); // 快捷键3-6
                    ConditionHintManager.ShowWrongCategory(item.DisplayName, categoryName, shortcutKey);

                    Debug.Log($"[ItemWheel] ❌ 阻止设置不匹配物品: {item.DisplayName} → 快捷键{shortcutKey}({categoryName})");

                    __result = false;
                    return false; // 阻止原方法执行
                }

                // 匹配：允许设置继续
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemWheel] ItemShortcut.Set 补丁失败: {ex.Message}");
                return true; // 出错时允许原方法执行
            }
        }
        /// <summary>
        /// 补丁 IsItemValid 方法，扩展验证逻辑
        /// 原方法只允许主背包物品，我们扩展为支持：
        /// 1. 主背包物品
        /// 2. 主背包容器中的物品
        /// 3. 宠物背包物品
        /// 4. 宠物背包容器中的物品
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("IsItemValid")]
        private static bool IsItemValid_Prefix(Item item, ref bool __result)
        {
            try
            {
                // 基础检查：物品不为空
                if (item == null)
                {
                    __result = false;
                    return false; // 跳过原方法
                }

                // 武器不能设置到快捷栏（保持原逻辑）
                if (item.Tags != null && item.Tags.Contains("Weapon"))
                {
                    __result = false;
                    return false;
                }

                // 获取主角色和主背包
                var character = CharacterMainControl.Main;
                if (character == null || character.CharacterItem == null)
                {
                    __result = false;
                    return false;
                }

                var mainInventory = character.CharacterItem.Inventory;
                if (mainInventory == null)
                {
                    __result = false;
                    return false;
                }

                // 🆕 扩展验证逻辑：支持多种来源
                bool isValid = false;

                // 1. 检查是否在主背包
                if (item.InInventory == mainInventory)
                {
                    isValid = true;
                }
                // 2. 检查是否在主背包的容器中
                else if (IsItemInContainerOf(item, mainInventory))
                {
                    isValid = true;
                }
                // 3. 检查是否在宠物背包
                else if (PetProxy.PetInventory != null && item.InInventory == PetProxy.PetInventory)
                {
                    isValid = true;
                }
                // 4. 检查是否在宠物背包的容器中
                else if (PetProxy.PetInventory != null && IsItemInContainerOf(item, PetProxy.PetInventory))
                {
                    isValid = true;
                }
                // 5. 插在任意槽位（例如 CashSlot 的自定义槽）
                else if (item.PluggedIntoSlot != null)
                {
                    isValid = true;
                }

                __result = isValid;
                return false; // 跳过原方法，使用我们的结果
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemWheel] ItemShortcut.IsItemValid 补丁失败: {ex.Message}");
                // 出错时使用原方法
                return true;
            }
        }

        /// <summary>
        /// 检查物品是否在指定背包的某个容器中
        /// </summary>
        private static bool IsItemInContainerOf(Item item, Inventory inventory)
        {
            if (inventory?.Content == null)
                return false;

            foreach (var containerItem in inventory.Content)
            {
                if (containerItem?.Slots == null)
                    continue;

                foreach (var slot in containerItem.Slots)
                {
                    if (slot?.Content == item)
                        return true;
                }
            }

            return false;
        }
    }
}
