using System;
using UnityEngine;

namespace ItemWheel
{
    /// <summary>
    /// ItemWheel配置数据类，包含所有可配置的设置项
    /// 提供默认值，确保即使ModSetting不可用也能正常工作
    /// </summary>
    [Serializable]
    public class ItemWheelModSettings
    {
        // ███ 搜索设置 ███
        [Header("搜索设置")]
        [Tooltip("搜索背包中容器内的物品（如背包中的弹匣）")]
        public bool SearchInSlots = true;

        [Tooltip("搜索宠物背包中的物品")]
        public bool SearchInPetInventory = true;

        // ███ 轮盘类别开关 ███
        [Header("轮盘类别")]
        [Tooltip("启用医疗品轮盘（快捷键3）")]
        public bool EnableMedicalWheel = true;

        [Tooltip("启用刺激物轮盘（快捷键4）")]
        public bool EnableStimWheel = true;

        [Tooltip("启用食物轮盘（快捷键5）")]
        public bool EnableFoodWheel = true;

        [Tooltip("启用手雷轮盘（快捷键6）")]
        public bool EnableExplosiveWheel = true;

        [Tooltip("启用近战武器轮盘（快捷键V）")]
        public bool EnableMeleeWheel = true;

        [Tooltip("启用手弹轮盘（长按R）")]
        public bool EnableAmmoWheel = true;

        // ███ 特殊功能 ███
        [Header("特殊功能")]
        [Tooltip("启用子弹时间功能（开发中）")]
        public bool EnableBulletTime = false;

        // ███ UI设置 ███
        [Header("UI设置")]
        [Tooltip("在轮盘上显示物品堆叠数量")]
        public bool ShowItemCount = true;

        [Tooltip("显示物品耐久条")]
        public bool ShowDurabilityBar = true;

        /// <summary>
        /// 检查某类轮盘是否启用
        /// </summary>
        public bool IsWheelEnabled(ItemWheelCategory category)
        {
            return category switch
            {
                ItemWheelCategory.Medical => EnableMedicalWheel,
                ItemWheelCategory.Stim => EnableStimWheel,
                ItemWheelCategory.Food => EnableFoodWheel,
                ItemWheelCategory.Explosive => EnableExplosiveWheel,
                ItemWheelCategory.Melee => EnableMeleeWheel,
                _ => true // 默认启用
            };
        }
    }
}
