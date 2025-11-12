using System;
using ItemStatsSystem;
using ItemWheel.Data;
using UnityEngine;

namespace ItemWheel.Handlers
{
    /// <summary>
    /// 默认物品处理器
    /// 用于医疗品、刺激物、食物等直接使用类物品
    /// </summary>
    public class DefaultItemHandler : IItemHandler
    {
        private readonly ItemWheelSystem.ItemWheelCategory _category;

        public ItemWheelSystem.ItemWheelCategory Category => _category;

        public DefaultItemHandler(ItemWheelSystem.ItemWheelCategory category)
        {
            _category = category;
        }

        public void UseItem(Item item, CharacterMainControl character, CategoryWheel wheel)
        {
            if (item?.UsageUtilities != null && item.UsageUtilities.IsUsable(item, character))
            {
                // 🆕 使用物品前，订阅销毁事件（用于容器物品消失后刷新快捷栏）
                ItemWheelSystem.SubscribeToItemDestroy(item);

                character.UseItem(item);
                // 使用成功（满足 IsUsable）后，重置"不可使用"情绪计数回到平静
                try
                {
                    ConditionHintManager.Reset(ConditionHintManager.HintCondition.ItemNotUsable);
                }
                catch { }
            }
            else
            {
                Debug.Log($"[ItemWheel] Item {item?.DisplayName ?? "null"} cannot be used directly.");
                // 使用条件化提示：多套文案 + 情绪升级 + 轮换
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

        public void OnItemSelected(Item item, int index, CategoryWheel wheel)
        {
            // 默认逻辑：选中即使用并关闭轮盘
            // 由ItemWheelSystem统一处理
        }

        public void OnWheelShown(CategoryWheel wheel)
        {
            // 默认无特殊逻辑
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
    }
}
