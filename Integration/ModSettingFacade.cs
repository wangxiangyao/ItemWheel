using System;
using Duckov.Modding;
using UnityEngine;

namespace ItemWheel.Integration
{
    /// <summary>
    /// ModSetting 统一门面
    /// 提供统一的配置访问接口，自动检测ModSetting是否可用
    /// 如果ModSetting不可用，则使用默认配置
    /// </summary>
    public static class ModSettingFacade
    {
        private static ItemWheelModSettings _settings;
        private static bool _isInitialized = false;
        private static bool _isModSettingAvailable = false;
        private static ModInfo _modInfo;

        /// <summary>
        /// ModSetting是否可用
        /// </summary>
        public static bool IsModSettingAvailable => _isModSettingAvailable;

        /// <summary>
        /// 当前配置（如果ModSetting不可用，返回默认配置）
        /// </summary>
        public static ItemWheelModSettings Settings
        {
            get
            {
                if (!_isInitialized)
                {
                    Debug.LogWarning("[ItemWheel] ModSettingFacade not initialized, using default settings");
                    return ItemWheelModSettings.CreateDefault();
                }
                return _settings;
            }
        }

        /// <summary>
        /// 初始化 ModSettingFacade
        /// </summary>
        /// <param name="modInfo">模组信息</param>
        public static void Initialize(ModInfo modInfo)
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[ItemWheel] ModSettingFacade already initialized");
                return;
            }

            _modInfo = modInfo;
            _settings = ItemWheelModSettings.CreateDefault();

            // 尝试初始化 ModSetting
            try
            {
                _isModSettingAvailable = ModSettingAPI.Init(modInfo);

                if (_isModSettingAvailable)
                {
                    Debug.Log("[ItemWheel] ModSetting available, loading saved values...");

                    // 🆕 关键修复：先加载保存的值到_settings对象
                    LoadSavedSettings();

                    Debug.Log("[ItemWheel] Registering UI with loaded settings...");
                    RegisterModSettingUI();
                    Debug.Log("[ItemWheel] ModSetting initialized successfully");
                }
                else
                {
                    Debug.Log("[ItemWheel] ModSetting not available, using default settings");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemWheel] Failed to initialize ModSetting: {ex.Message}");
                _isModSettingAvailable = false;
            }

            _isInitialized = true;

            // 打印当前配置
            Debug.Log($"[ItemWheel] Current settings:\n{_settings}");
        }

        /// <summary>
        /// 注册 ModSetting UI
        /// </summary>
        private static void RegisterModSettingUI()
        {
            try
            {
                // ==================== 搜索设置 ====================

                ModSettingAPI.AddToggle(
                    "ItemWheel_SearchInSlots",
                    "搜索容器内的物品",
                    GetSavedValue("ItemWheel_SearchInSlots", _settings.SearchInSlots),
                    value => _settings.SearchInSlots = value
                );

                ModSettingAPI.AddToggle(
                    "ItemWheel_SearchInPetInventory",
                    "搜索宠物背包",
                    GetSavedValue("ItemWheel_SearchInPetInventory", _settings.SearchInPetInventory),
                    value => _settings.SearchInPetInventory = value
                );

                // ==================== 轮盘类别开关 ====================

                ModSettingAPI.AddToggle(
                    "ItemWheel_EnableMedical",
                    "医疗品轮盘 (3)",
                    GetSavedValue("ItemWheel_EnableMedical", _settings.EnableMedicalWheel),
                    value =>
                    {
                        Debug.Log($"[ItemWheel] 设置更新: EnableMedicalWheel = {value}");
                        _settings.EnableMedicalWheel = value;
                    }
                );

                ModSettingAPI.AddToggle(
                    "ItemWheel_EnableStim",
                    "刺激物轮盘 (4)",
                    GetSavedValue("ItemWheel_EnableStim", _settings.EnableStimWheel),
                    value =>
                    {
                        Debug.Log($"[ItemWheel] 设置更新: EnableStimWheel = {value}");
                        _settings.EnableStimWheel = value;
                    }
                );

                ModSettingAPI.AddToggle(
                    "ItemWheel_EnableFood",
                    "食物轮盘 (5)",
                    GetSavedValue("ItemWheel_EnableFood", _settings.EnableFoodWheel),
                    value =>
                    {
                        Debug.Log($"[ItemWheel] 设置更新: EnableFoodWheel = {value}");
                        _settings.EnableFoodWheel = value;
                    }
                );

                ModSettingAPI.AddToggle(
                    "ItemWheel_EnableExplosive",
                    "手雷轮盘 (6)",
                    GetSavedValue("ItemWheel_EnableExplosive", _settings.EnableExplosiveWheel),
                    value =>
                    {
                        Debug.Log($"[ItemWheel] 设置更新: EnableExplosiveWheel = {value}");
                        _settings.EnableExplosiveWheel = value;
                    }
                );

                ModSettingAPI.AddToggle(
                    "ItemWheel_EnableMelee",
                    "近战武器轮盘 (V)",
                    GetSavedValue("ItemWheel_EnableMelee", _settings.EnableMeleeWheel),
                    value =>
                    {
                        Debug.Log($"[ItemWheel] 设置更新: EnableMeleeWheel = {value}");
                        _settings.EnableMeleeWheel = value;
                    }
                );

                ModSettingAPI.AddToggle(
                    "ItemWheel_EnableGun",
                    "枪械轮盘 (长按1/2)",
                    GetSavedValue("ItemWheel_EnableGun", _settings.EnableGunWheel),
                    value =>
                    {
                        Debug.Log($"[ItemWheel] 设置更新: EnableGunWheel = {value}");
                        _settings.EnableGunWheel = value;
                    }
                );

                ModSettingAPI.AddToggle(
                    "ItemWheel_IncludeEquippedGuns",
                    "枪械轮盘包含装备栏武器",
                    GetSavedValue("ItemWheel_IncludeEquippedGuns", _settings.IncludeEquippedGuns),
                    value => _settings.IncludeEquippedGuns = value
                );

                ModSettingAPI.AddToggle(
                    "ItemWheel_SelectLastUsedItem",
                    "使用后选中最近物品",
                    GetSavedValue("ItemWheel_SelectLastUsedItem", _settings.SelectLastUsedItem),
                    value => _settings.SelectLastUsedItem = value
                );

                ModSettingAPI.AddToggle(
                    "ItemWheel_EnableAmmo",
                    "子弹轮盘 (长按R)",
                    GetSavedValue("ItemWheel_EnableAmmo", _settings.EnableAmmoWheel),
                    value => _settings.EnableAmmoWheel = value
                );

                // ==================== UI设置 ====================

                ModSettingAPI.AddToggle(
                    "ItemWheel_ShowItemCount",
                    "显示物品数量",
                    GetSavedValue("ItemWheel_ShowItemCount", _settings.ShowItemCount),
                    value => _settings.ShowItemCount = value
                );

                ModSettingAPI.AddToggle(
                    "ItemWheel_ShowDurabilityBar",
                    "显示耐久条",
                    GetSavedValue("ItemWheel_ShowDurabilityBar", _settings.ShowDurabilityBar),
                    value => _settings.ShowDurabilityBar = value
                );

                ModSettingAPI.AddToggle(
                    "ItemWheel_ShowName",
                    "显示物品名称",
                    GetSavedValue("ItemWheel_ShowName", _settings.ShowName),
                    value => _settings.ShowName = value
                );

                ModSettingAPI.AddToggle(
                    "ItemWheel_ShowRightText",
                    "显示右侧文字",
                    GetSavedValue("ItemWheel_ShowRightText", _settings.ShowRightText),
                    value => _settings.ShowRightText = value
                );

                // ==================== 特殊功能 ====================

                ModSettingAPI.AddToggle(
                    "ItemWheel_EnableBulletTime",
                    "启用子弹时间（打开轮盘时减速）",
                    GetSavedValue("ItemWheel_EnableBulletTime", _settings.EnableBulletTime),
                    value => _settings.EnableBulletTime = value
                );

                ModSettingAPI.AddSlider(
                    "ItemWheel_BulletTimeScale",
                    "时间流速 (0.1=极慢, 1.0=正常)",
                    GetSavedValue("ItemWheel_BulletTimeScale", _settings.BulletTimeScale),
                    new UnityEngine.Vector2(0.1f, 1.0f),
                    value => _settings.BulletTimeScale = value,
                    2  // 保留2位小数
                );

                ModSettingAPI.AddSlider(
                    "ItemWheel_BulletTimeTransitionSpeed",
                    "音效过渡速度 (1-10，越大越快)",
                    GetSavedValue("ItemWheel_BulletTimeTransitionSpeed", _settings.BulletTimeTransitionSpeed),
                    new UnityEngine.Vector2(1f, 10f),
                    value => _settings.BulletTimeTransitionSpeed = value,
                    1  // 保留1位小数
                );

                ModSettingAPI.AddToggle(
                    "ItemWheel_BulletTimeAdjustAudioPitch",
                    "音效变低沉（更有慢动作感）",
                    GetSavedValue("ItemWheel_BulletTimeAdjustAudioPitch", _settings.BulletTimeAdjustAudioPitch),
                    value => _settings.BulletTimeAdjustAudioPitch = value
                );

                Debug.Log("[ItemWheel] ModSetting UI registered successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemWheel] Failed to register ModSetting UI: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 获取已保存的配置值
        /// </summary>
        private static T GetSavedValue<T>(string key, T defaultValue)
        {
            if (!_isModSettingAvailable)
            {
                return defaultValue;
            }

            try
            {
                if (ModSettingAPI.GetSavedValue<T>(key, out T savedValue))
                {
                    return savedValue;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ItemWheel] Failed to get saved value for '{key}': {ex.Message}");
            }

            return defaultValue;
        }

        /// <summary>
        /// 🆕 加载保存的配置到_settings对象（初始化时调用）
        /// </summary>
        private static void LoadSavedSettings()
        {
            if (!_isModSettingAvailable)
            {
                Debug.Log("[ItemWheel] ModSetting not available, skipping load");
                return;
            }

            // 读取所有保存的值
            _settings.SearchInSlots = GetSavedValue("ItemWheel_SearchInSlots", true);
            _settings.SearchInPetInventory = GetSavedValue("ItemWheel_SearchInPetInventory", true);
            _settings.EnableMedicalWheel = GetSavedValue("ItemWheel_EnableMedical", true);
            _settings.EnableStimWheel = GetSavedValue("ItemWheel_EnableStim", true);
            _settings.EnableFoodWheel = GetSavedValue("ItemWheel_EnableFood", true);
            _settings.EnableExplosiveWheel = GetSavedValue("ItemWheel_EnableExplosive", true);
            _settings.EnableMeleeWheel = GetSavedValue("ItemWheel_EnableMelee", true);
            _settings.EnableGunWheel = GetSavedValue("ItemWheel_EnableGun", true);
            _settings.IncludeEquippedGuns = GetSavedValue("ItemWheel_IncludeEquippedGuns", false);
            _settings.SelectLastUsedItem = GetSavedValue("ItemWheel_SelectLastUsedItem", false);
            _settings.EnableAmmoWheel = GetSavedValue("ItemWheel_EnableAmmo", true);
            _settings.ShowItemCount = GetSavedValue("ItemWheel_ShowItemCount", true);
            _settings.ShowDurabilityBar = GetSavedValue("ItemWheel_ShowDurabilityBar", true);
            _settings.ShowName = GetSavedValue("ItemWheel_ShowName", true);
            _settings.ShowRightText = GetSavedValue("ItemWheel_ShowRightText", true);
            _settings.EnableBulletTime = GetSavedValue("ItemWheel_EnableBulletTime", false);
            _settings.BulletTimeScale = GetSavedValue("ItemWheel_BulletTimeScale", 0.3f);
            _settings.BulletTimeTransitionSpeed = GetSavedValue("ItemWheel_BulletTimeTransitionSpeed", 5.0f);
            _settings.BulletTimeAdjustAudioPitch = GetSavedValue("ItemWheel_BulletTimeAdjustAudioPitch", true);

            Debug.Log($"[ItemWheel] Settings loaded from config:\n{_settings}");
        }

        /// <summary>
        /// 重新加载配置（用于调试）
        /// </summary>
        public static void ReloadSettings()
        {
            if (!_isInitialized || !_isModSettingAvailable)
            {
                Debug.LogWarning("[ItemWheel] Cannot reload settings: not initialized or ModSetting not available");
                return;
            }

            Debug.Log("[ItemWheel] Reloading settings...");
            LoadSavedSettings();
            Debug.Log($"[ItemWheel] Settings reloaded:\n{_settings}");
        }
    }
}
