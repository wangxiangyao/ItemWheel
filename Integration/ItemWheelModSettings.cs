using System;

namespace ItemWheel.Integration
{
    /// <summary>
    /// ItemWheel 的所有配置项
    /// 提供默认值确保向后兼容
    /// </summary>
    public class ItemWheelModSettings
    {
        // ==================== 搜索设置 ====================

        /// <summary>
        /// 是否搜索容器内的物品（如背包中的箱子）
        /// </summary>
        public bool SearchInSlots { get; set; } = true;

        /// <summary>
        /// 是否搜索宠物背包
        /// </summary>
        public bool SearchInPetInventory { get; set; } = true;

        // ==================== 轮盘类别开关 ====================

        /// <summary>
        /// 医疗品轮盘 (快捷键3)
        /// </summary>
        public bool EnableMedicalWheel { get; set; } = true;

        /// <summary>
        /// 刺激物轮盘 (快捷键4)
        /// </summary>
        public bool EnableStimWheel { get; set; } = true;

        /// <summary>
        /// 食物轮盘 (快捷键5)
        /// </summary>
        public bool EnableFoodWheel { get; set; } = true;

        /// <summary>
        /// 手雷轮盘 (快捷键6)
        /// </summary>
        public bool EnableExplosiveWheel { get; set; } = true;

        /// <summary>
        /// 近战武器轮盘 (快捷键V)
        /// </summary>
        public bool EnableMeleeWheel { get; set; } = true;

        /// <summary>
        /// 枪械轮盘 (长按1/2)
        /// </summary>
        public bool EnableGunWheel { get; set; } = true;

        /// <summary>
        /// 是否将装备栏中的枪械纳入轮盘
        /// </summary>
        public bool IncludeEquippedGuns { get; set; } = false;

        /// <summary>
        /// 子弹轮盘 (长按R)
        /// </summary>
        public bool EnableAmmoWheel { get; set; } = true;

        // ==================== UI设置 ====================

        /// <summary>
        /// 显示物品数量
        /// </summary>
        public bool ShowItemCount { get; set; } = true;

        /// <summary>
        /// 显示耐久条
        /// </summary>
        public bool ShowDurabilityBar { get; set; } = true;

        /// <summary>
        /// 显示物品名称
        /// </summary>
        public bool ShowName { get; set; } = true;

        /// <summary>
        /// 显示右侧文字（如弹药数量）
        /// </summary>
        public bool ShowRightText { get; set; } = true;

        // ==================== 特殊功能 ====================

        /// <summary>
        /// 启用子弹时间效果（打开轮盘时减慢游戏速度）
        /// </summary>
        public bool EnableBulletTime { get; set; } = false;

        /// <summary>
        /// 子弹时间的速度缩放比例 (0.1 = 10%速度, 1.0 = 正常速度)
        /// 推荐值: 0.3 (30%速度)
        /// </summary>
        public float BulletTimeScale { get; set; } = 0.3f;

        /// <summary>
        /// 子弹时间过渡速度（数值越大切换越快）
        /// 推荐值: 5.0
        /// </summary>
        public float BulletTimeTransitionSpeed { get; set; } = 5.0f;

        /// <summary>
        /// 是否根据时间缩放调整音效音调
        /// 启用后音效会变得低沉，更有"慢动作"感觉
        /// </summary>
        public bool BulletTimeAdjustAudioPitch { get; set; } = true;

        // ==================== 辅助方法 ====================

        /// <summary>
        /// 检查指定类别的轮盘是否启用
        /// </summary>
        public bool IsWheelEnabled(ItemWheelSystem.ItemWheelCategory category)
        {
            return category switch
            {
                ItemWheelSystem.ItemWheelCategory.Medical => EnableMedicalWheel,
                ItemWheelSystem.ItemWheelCategory.Stim => EnableStimWheel,
                ItemWheelSystem.ItemWheelCategory.Food => EnableFoodWheel,
                ItemWheelSystem.ItemWheelCategory.Explosive => EnableExplosiveWheel,
                ItemWheelSystem.ItemWheelCategory.Melee => EnableMeleeWheel,
                ItemWheelSystem.ItemWheelCategory.Gun => EnableGunWheel,
                _ => true
            };
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static ItemWheelModSettings CreateDefault()
        {
            return new ItemWheelModSettings();
        }

        /// <summary>
        /// 打印当前配置（用于调试）
        /// </summary>
        public override string ToString()
        {
            return $"ItemWheelSettings[\n" +
                   $"  SearchInSlots={SearchInSlots}\n" +
                   $"  SearchInPetInventory={SearchInPetInventory}\n" +
                   $"  Medical={EnableMedicalWheel}, Stim={EnableStimWheel}, Food={EnableFoodWheel}\n" +
                   $"  Explosive={EnableExplosiveWheel}, Melee={EnableMeleeWheel}, Gun={EnableGunWheel}, Ammo={EnableAmmoWheel}\n" +
                   $"  IncludeEquippedGuns={IncludeEquippedGuns}\n" +
                   $"]";
        }
    }
}
