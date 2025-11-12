using System;
using ItemStatsSystem;
using ItemWheel.Data;
using UnityEngine;

namespace ItemWheel.Handlers
{
    /// <summary>
    /// 手雷处理器
    /// 特殊逻辑：堆叠显示、从后往前使用
    /// </summary>
    public class ExplosiveHandler : IItemHandler
    {
        public ItemWheelSystem.ItemWheelCategory Category => ItemWheelSystem.ItemWheelCategory.Explosive;

        public void UseItem(Item item, CharacterMainControl character, CategoryWheel wheel)
        {
            if (item == null || character == null) return;

            // 🆕 手雷特殊处理：选择最后一个手雷装备（从后往前使用）
            Item grenadeToEquip = null;

            if (wheel?.ItemInfoMap != null)
            {
                // 找相同类型的手雷堆叠
                string targetTypeId = item.TypeID.ToString();

                foreach (var kvp in wheel.ItemInfoMap)
                {
                    if (kvp.Value.Item != null && kvp.Value.Item.TypeID.ToString() == targetTypeId)
                    {
                        // 选择最后一个手雷
                        if (kvp.Value.AllLocations != null && kvp.Value.AllLocations.Count > 0)
                        {
                            int lastIndex = kvp.Value.AllLocations.Count - 1;
                            ItemLocation lastLocation = kvp.Value.AllLocations[lastIndex];

                            // 🆕 从正确的背包获取手雷（支持主背包/宠物背包/容器）
                            if (lastLocation.Inventory?.Content != null &&
                                lastLocation.BackpackIndex >= 0 &&
                                lastLocation.BackpackIndex < lastLocation.Inventory.Content.Count)
                            {
                                grenadeToEquip = lastLocation.Inventory.Content[lastLocation.BackpackIndex];
                                Debug.Log($"[ItemWheel] 💣 选择最后一个手雷装备: {grenadeToEquip?.DisplayName}, 位置={lastLocation}");
                            }
                        }
                        break;
                    }
                }
            }

            // 装备找到的手雷，如果没有找到则装备传入的 item
            Item equipItem = grenadeToEquip ?? item;
            EquipGrenadeToHand(equipItem, character);
            Debug.Log($"[ItemWheel] 已装备手雷: {equipItem.DisplayName}");
        }

        public void OnItemSelected(Item item, int index, CategoryWheel wheel)
        {
            // 手雷选中即装备并关闭轮盘
            // 由ItemWheelSystem统一处理
        }

        public void OnWheelShown(CategoryWheel wheel)
        {
            // 手雷无特殊显示逻辑
        }

        public int GetPreferredIndex(CategoryWheel wheel)
        {
            // 默认：返回第一个非空槽位
            if (wheel?.Slots == null) return -1;

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
        /// 装备手雷到手上
        /// </summary>
        private static void EquipGrenadeToHand(Item item, CharacterMainControl character)
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

            var result = holder.ChangeHoldItem(item);
            if (result == null)
            {
                // 如果无法装备到手上，则尝试直接使用
                TryUseGrenadeDirectly(item, character);
            }
        }

        /// <summary>
        /// 直接使用手雷（备用方案）
        /// </summary>
        private static void TryUseGrenadeDirectly(Item item, CharacterMainControl character)
        {
            if (item?.UsageUtilities != null && item.UsageUtilities.IsUsable(item, character))
            {
                character.UseItem(item);
                try
                {
                    ConditionHintManager.Reset(ConditionHintManager.HintCondition.ItemNotUsable);
                }
                catch { }
            }
            else
            {
                Debug.Log($"[ItemWheel] Grenade {item?.DisplayName ?? "null"} cannot be used.");
                try
                {
                    ConditionHintManager.ShowItemNotUsable(item?.DisplayName ?? "该物品");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ItemWheel] Show not-usable hint failed: {e.Message}");
                }
            }
        }
    }
}
