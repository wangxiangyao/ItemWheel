using System;
using System.Reflection;
using Duckov.Modding;
using UnityEngine;

namespace ItemWheel
{
    /// <summary>
    /// ModSetting兼容层门面类
    /// 同时支持ModSetting和默认配置，优先使用ModSetting，如果不可用则使用默认值
    /// </summary>
    public static class ModSettingFacade
    {
        // ModSetting是否可用
        public static bool IsModSettingAvailable { get; private set; } = false;

        // 默认配置实例（作为后备）
        private static readonly ItemWheelModSettings _defaultSettings = new ItemWheelModSettings();

        // 当前使用的配置（可能来自ModSetting或默认值）
        public static ItemWheelModSettings Settings { get; private set; }

        // ModInfo引用
        private static ModInfo _modInfo;

        // 是否已经初始化
        private static bool _isInitialized = false;

        /// <summary>
        /// 初始化ModSettingFacade
        /// 会尝试加载ModSetting，如果失败则使用默认配置
        /// </summary>
        public static void Initialize(ModInfo modInfo)
        {
            if (_isInitialized) return;

            _modInfo = modInfo;
            Settings = new ItemWheelModSettings();

            try
            {
                // 尝试加载ModSetting
                if (TryLoadModSetting())
                {
                    Debug.Log("[ItemWheel] ModSetting初始化成功，已启用配置面板");
                    IsModSettingAvailable = true;

                    // 从ModSetting读取保存的值
                    LoadSettingsFromModSetting();

                    // 在ModSetting中注册UI控件
                    RegisterModSettingUI();

                    // 订阅设置变更事件
                    SubscribeToSettingChanges();
                }
                else
                {
                    Debug.LogWarning("[ItemWheel] ModSetting不可用，使用默认配置");
                    IsModSettingAvailable = false;
                    Settings = _defaultSettings;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemWheel] 初始化ModSetting失败: {ex.Message}\n{ex.StackTrace}");
                IsModSettingAvailable = false;
                Settings = _defaultSettings;
            }

            _isInitialized = true;
        }

        /// <summary>
        /// 尝试加载ModSetting
        /// </summary>
        private static bool TryLoadModSetting()
        {
            try
            {
                // 检查ModSettingAPI是否存在
                var modSettingType = Type.GetType("ModSettingAPI, Assembly-CSharp");
                if (modSettingType == null)
                {
                    Debug.LogWarning("[ItemWheel] ModSettingAPI类型未找到，可能未安装ModSetting Mod");
                    return false;
                }

                // 检查IsInit属性
                var isInitProperty = modSettingType.GetProperty("IsInit", BindingFlags.Public | BindingFlags.Static);
                if (isInitProperty != null)
                {
                    bool isInit = (bool)isInitProperty.GetValue(null);
                    if (!isInit)
                    {
                        // 尝试初始化
                        var initMethod = modSettingType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static);
                        if (initMethod != null)
                        {
                            bool result = (bool)initMethod.Invoke(null, new object[] { _modInfo });
                            if (!result)
                            {
                                Debug.LogWarning("[ItemWheel] ModSetting初始化失败");
                                return false;
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ItemWheel] 检查ModSetting时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从ModSetting读取保存的值
        /// </summary>
        private static void LoadSettingsFromModSetting()
        {
            try
            {
                // 读取搜索设置
                GetModSettingValue("ItemWheel_SearchInSlots", ref Settings.SearchInSlots);
                GetModSettingValue("ItemWheel_SearchInPetInventory", ref Settings.SearchInPetInventory);

                // 读取轮盘开关
                GetModSettingValue("ItemWheel_EnableMedical", ref Settings.EnableMedicalWheel);
                GetModSettingValue("ItemWheel_EnableStim", ref Settings.EnableStimWheel);
                GetModSettingValue("ItemWheel_EnableFood", ref Settings.EnableFoodWheel);
                GetModSettingValue("ItemWheel_EnableExplosive", ref Settings.EnableExplosiveWheel);
                GetModSettingValue("ItemWheel_EnableMelee", ref Settings.EnableMeleeWheel);
                GetModSettingValue("ItemWheel_EnableAmmo", ref Settings.EnableAmmoWheel);

                // 读取特殊功能
                GetModSettingValue("ItemWheel_EnableBulletTime", ref Settings.EnableBulletTime);

                // 读取UI设置
                GetModSettingValue("ItemWheel_ShowItemCount", ref Settings.ShowItemCount);
                GetModSettingValue("ItemWheel_ShowDurabilityBar", ref Settings.ShowDurabilityBar);

                Debug.Log("[ItemWheel] 配置加载完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemWheel] 从ModSetting加载配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从ModSetting读取值（如果存在）
        /// </summary>
        private static void GetModSettingValue<T>(string key, ref T value)
        {
            try
            {
                // 检查是否有保存的值
                if (HasSavedValue<T>(key, out var savedValue))
                {
                    value = savedValue;
                    Debug.Log($"[ItemWheel] 读取配置 {key} = {value}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ItemWheel] 读取配置失败 {key}: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查ModSetting是否有保存的值
        /// </summary>
        private static bool HasSavedValue<T>(string key, out T value)
        {
            value = default;
            try
            {
                var modSettingType = Type.GetType("ModSettingAPI, Assembly-CSharp");
                if (modSettingType == null) return false;

                var method = modSettingType.GetMethod("GetSavedValue", BindingFlags.Public | BindingFlags.Static);
                if (method == null) return false;

                var genericMethod = method.MakeGenericMethod(typeof(T));
                var parameters = new object[] { _modInfo, key, null };

                bool hasValue = (bool)genericMethod.Invoke(null, parameters);
                if (hasValue)
                {
                    value = (T)parameters[2];
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ItemWheel] 检查配置存在失败 {key}: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 在ModSetting中注册UI控件
        /// </summary>
        private static void RegisterModSettingUI()
        {
            try
            {
                var modSettingType = Type.GetType("ModSettingAPI, Assembly-CSharp");
                if (modSettingType == null) return;

                // ███ 搜索设置分组 ███
                AddGroup("ItemWheel_Search_Group", "搜索设置",
                    new[] { "ItemWheel_SearchInSlots", "ItemWheel_SearchInPetInventory" },
                    scale: 0.8f, open: true);

                AddToggle("ItemWheel_SearchInSlots", "搜索容器内的物品",
                    Settings.SearchInSlots, value => Settings.SearchInSlots = value);

                AddToggle("ItemWheel_SearchInPetInventory", "搜索宠物背包",
                    Settings.SearchInPetInventory, value => Settings.SearchInPetInventory = value);

                // ███ 轮盘类别分组 ███
                AddGroup("ItemWheel_Categories_Group", "轮盘类别",
                    new[] {
                        "ItemWheel_EnableMedical", "ItemWheel_EnableStim",
                        "ItemWheel_EnableFood", "ItemWheel_EnableExplosive",
                        "ItemWheel_EnableMelee", "ItemWheel_EnableAmmo"
                    }, scale: 0.8f, open: true);

                AddToggle("ItemWheel_EnableMedical", "医疗品轮盘 (3)",
                    Settings.EnableMedicalWheel, value => Settings.EnableMedicalWheel = value);

                AddToggle("ItemWheel_EnableStim", "刺激物轮盘 (4)",
                    Settings.EnableStimWheel, value => Settings.EnableStimWheel = value);

                AddToggle("ItemWheel_EnableFood", "食物轮盘 (5)",
                    Settings.EnableFoodWheel, value => Settings.EnableFoodWheel = value);

                AddToggle("ItemWheel_EnableExplosive", "手雷轮盘 (6)",
                    Settings.EnableExplosiveWheel, value => Settings.EnableExplosiveWheel = value);

                AddToggle("ItemWheel_EnableMelee", "近战武器轮盘 (V)",
                    Settings.EnableMeleeWheel, value => Settings.EnableMeleeWheel = value);

                AddToggle("ItemWheel_EnableAmmo", "子弹轮盘 (长按R)",
                    Settings.EnableAmmoWheel, value => Settings.EnableAmmoWheel = value);

                // ███ 特殊功能分组 ███
                AddGroup("ItemWheel_Features_Group", "特殊功能",
                    new[] { "ItemWheel_EnableBulletTime" },
                    scale: 0.8f, open: false);

                AddToggle("ItemWheel_EnableBulletTime", "子弹时间 (开发中)",
                    Settings.EnableBulletTime, value => Settings.EnableBulletTime = value);

                // ███ UI设置分组 ███
                AddGroup("ItemWheel_UI_Group", "界面设置",
                    new[] { "ItemWheel_ShowItemCount", "ItemWheel_ShowDurabilityBar" },
                    scale: 0.8f, open: false);

                AddToggle("ItemWheel_ShowItemCount", "显示物品数量",
                    Settings.ShowItemCount, value => Settings.ShowItemCount = value);

                AddToggle("ItemWheel_ShowDurabilityBar", "显示耐久条",
                    Settings.ShowDurabilityBar, value => Settings.ShowDurabilityBar = value);

                Debug.Log("[ItemWheel] 配置UI注册完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemWheel] 注册配置UI失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 订阅设置变更事件（自动保存）
        /// </summary>
        private static void SubscribeToSettingChanges()
        {
            // 当设置变更时自动保存到ModSetting
            // ModSetting会自动处理持久化
        }

        /// <summary>
        /// 添加分组（包装方法）
        </summary>
        private static void AddGroup(string key, string description, string[] keys, float scale, bool open)
        {
            try
            {
                var modSettingType = Type.GetType("ModSettingAPI, Assembly-CSharp");
                if (modSettingType == null) return;

                var method = modSettingType.GetMethod("AddGroup", BindingFlags.Public | BindingFlags.Static);
                if (method == null) return;

                var keysList = new System.Collections.Generic.List<string>(keys);
                method.Invoke(null, new object[] { _modInfo, key, description, keysList, scale, false, open });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ItemWheel] 添加分组失败 {key}: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加开关（包装方法）
        /// </summary>
        private static void AddToggle(string key, string description, bool defaultValue, Action<bool> onChange)
        {
            try
            {
                var modSettingType = Type.GetType("ModSettingAPI, Assembly-CSharp");
                if (modSettingType == null) return;

                var method = modSettingType.GetMethod("AddToggle", BindingFlags.Public | BindingFlags.Static);
                if (method == null) return;

                method.Invoke(null, new object[] { _modInfo, key, description, defaultValue, onChange });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ItemWheel] 添加开关失败 {key}: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加滑块（包装方法）
        /// </summary>
        private static void AddSlider(string key, string description, float defaultValue, Vector2 range, Action<float> onChange)
        {
            try
            {
                var modSettingType = Type.GetType("ModSettingAPI, Assembly-CSharp");
                if (modSettingType == null) return;

                var method = modSettingType.GetMethod("AddSlider", BindingFlags.Public | BindingFlags.Static,
                    null, new Type[] { typeof(ModInfo), typeof(string), typeof(string), typeof(float), typeof(Vector2), typeof(Action<float>), typeof(int), typeof(int) }, null);
                if (method == null) return;

                method.Invoke(null, new object[] { _modInfo, key, description, defaultValue, range, onChange, 1, 5 });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ItemWheel] 添加滑块失败 {key}: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加按键绑定（包装方法）
        /// </summary>
        private static void AddKeybinding(string key, string description, KeyCode defaultKey, Action<KeyCode> onChange)
        {
            try
            {
                var modSettingType = Type.GetType("ModSettingAPI, Assembly-CSharp");
                if (modSettingType == null) return;

                var method = modSettingType.GetMethod("AddKeybinding", BindingFlags.Public | BindingFlags.Static,
                    null, new Type[] { typeof(ModInfo), typeof(string), typeof(string), typeof(KeyCode), typeof(Action<KeyCode>) }, null);
                if (method == null) return;

                method.Invoke(null, new object[] { _modInfo, key, description, defaultKey, onChange });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ItemWheel] 添加按键绑定失败 {key}: {ex.Message}");
            }
        }
    }
}
