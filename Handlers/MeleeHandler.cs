using System;
using ItemStatsSystem;
using ItemWheel.Data;
using UnityEngine;

namespace ItemWheel.Handlers
{
    /// <summary>
    /// 近战武器处理器
    /// 特殊逻辑：装备到近战槽、hover即装备、默认选中当前装备
    /// </summary>
    public class MeleeHandler : IItemHandler
    {
        private readonly Func<Inventory> _getInventory;

        public ItemWheelSystem.ItemWheelCategory Category => ItemWheelSystem.ItemWheelCategory.Melee;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="getInventory">获取背包的委托</param>
        public MeleeHandler(Func<Inventory> getInventory)
        {
            _getInventory = getInventory;
        }

        public void UseItem(Item item, CharacterMainControl character, CategoryWheel wheel)
        {
            EquipMeleeItemToSlot(item, character);
        }

        public void OnItemSelected(Item item, int index, CategoryWheel wheel)
        {
            // 近战：hover/选中即刻装备
            // 这个方法会在点击选中或确认时调用
        }

        public void OnWheelShown(CategoryWheel wheel)
        {
            // 近战显示时，设置默认选中为当前装备的近战武器
            SetMeleeDefaultSelection(wheel);
        }

        public int GetPreferredIndex(CategoryWheel wheel)
        {
            // 返回当前装备的近战武器索引
            if (wheel?.Slots == null) return -1;

            var character = CharacterMainControl.Main;
            if (character == null) return -1;

            // 1. 检查当前手持的是否是近战武器
            Item equipped = null;
            if (character.CurrentHoldItemAgent?.Item != null &&
                ItemWheelSystem.MatchesCategoryStatic(character.CurrentHoldItemAgent.Item, ItemWheelSystem.ItemWheelCategory.Melee))
            {
                equipped = character.CurrentHoldItemAgent.Item;
            }
            else
            {
                // 2. 检查近战槽内的武器
                var meleeSlot = character.MeleeWeaponSlot();
                equipped = meleeSlot?.Content;
            }

            // 3. 在槽位中查找
            if (equipped != null)
            {
                int idx = Array.IndexOf(wheel.Slots, equipped);
                if (idx >= 0)
                {
                    return idx;
                }
            }

            // 4. 默认返回第一个非空槽位
            for (int i = 0; i < wheel.Slots.Length; i++)
            {
                if (i == 8) continue; // 跳过中心槽位
                if (wheel.Slots[i] != null)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 装备近战武器到近战槽
        /// 将物品插入近战槽，并持有到手上；若槽内有旧物，回收到背包
        /// </summary>
        private void EquipMeleeItemToSlot(Item item, CharacterMainControl character)
        {
            if (item == null || character == null)
            {
                return;
            }

            try
            {
                var meleeSlot = character.MeleeWeaponSlot();
                if (meleeSlot == null)
                {
                    // 如果没有近战槽，直接装备到手上
                    EquipToHand(item, character);
                    return;
                }

                // 已在槽且已持有则不重复
                if (meleeSlot.Content == item &&
                    character.CurrentHoldItemAgent != null &&
                    character.CurrentHoldItemAgent.Item == item)
                {
                    return;
                }

                // 插入近战槽（自动处理从背包/其他槽脱离），取出旧物
                Item unplugged;
                bool plugged = meleeSlot.Plug(item, out unplugged);
                if (!plugged)
                {
                    // 插入失败：兜底仅持有
                    EquipToHand(item, character);
                    return;
                }

                // 旧物回收至背包
                if (unplugged != null)
                {
                    try
                    {
                        var inventory = _getInventory?.Invoke();
                        inventory?.AddItem(unplugged);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MeleeHandler] 回收旧物失败: {ex.Message}");
                    }
                }

                // 切换持有
                character.ChangeHoldItem(item);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MeleeHandler] EquipMeleeItem 异常: {ex.Message}");
                try { character.ChangeHoldItem(item); } catch { }
            }
        }

        /// <summary>
        /// 装备物品到手上（备用方案）
        /// </summary>
        private static void EquipToHand(Item item, CharacterMainControl character)
        {
            if (item == null || character == null)
            {
                return;
            }

            if (character.CurrentHoldItemAgent != null && character.CurrentHoldItemAgent.Item == item)
            {
                return;
            }

            var holder = character.agentHolder;
            if (holder == null)
            {
                return;
            }

            holder.ChangeHoldItem(item);
        }

        /// <summary>
        /// 设置默认选中为当前装备的近战武器
        /// </summary>
        private void SetMeleeDefaultSelection(CategoryWheel wheel)
        {
            if (wheel == null || wheel.Slots == null)
            {
                return;
            }

            try
            {
                var character = CharacterMainControl.Main;
                Item equipped = null;

                // 检查当前手持的是否是近战武器
                if (character?.CurrentHoldItemAgent?.Item != null &&
                    ItemWheelSystem.MatchesCategoryStatic(character.CurrentHoldItemAgent.Item, ItemWheelSystem.ItemWheelCategory.Melee))
                {
                    equipped = character.CurrentHoldItemAgent.Item;
                }
                else
                {
                    // 检查近战槽内的武器
                    var meleeSlot = character?.MeleeWeaponSlot();
                    equipped = meleeSlot?.Content;
                }

                if (equipped != null)
                {
                    int idx = Array.IndexOf(wheel.Slots, equipped);
                    if (idx >= 0)
                    {
                        wheel.LastConfirmedIndex = idx;
                        Debug.Log($"[MeleeHandler] 默认选中当前装备: {equipped.DisplayName}, 位置={idx}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MeleeHandler] SetMeleeDefaultSelection 异常: {ex.Message}");
            }
        }
    }
}
