using System;
using System.Collections.Generic;
using System.Linq;
using Duckov;
using ItemStatsSystem;
using QuickWheel.Core;
using QuickWheel.UI;
using QuickWheel.Utils;
using QuickWheel.Selection;
using UnityEngine;
using ItemWheel.UI;
using ItemWheel.Data;
using ItemWheel.Core;
using ItemWheel.Integration;
using ItemWheel.Features.BulletTime;
using ItemWheel.Features.BulletHUD;

namespace ItemWheel
{
    /// <summary>
    /// Item wheel system built on QuickWheel with four categories (meds, stims, food, explosives).
    /// 负责物品管理和轮盘业务逻辑，不处理按键输入
    /// </summary>
    public sealed class ItemWheelSystem : IDisposable
    {
        public enum ItemWheelCategory
        {
            Medical = 0,
            Stim = 1,
            Food = 2,
            Explosive = 3,
            Melee = 4
        }

        // 删除AllCategories数组，未使用

        private static readonly Dictionary<string, ItemWheelCategory> TagMappings =
            new Dictionary<string, ItemWheelCategory>(StringComparer.OrdinalIgnoreCase)
            {
                { "Healing", ItemWheelCategory.Medical },
                { "Injector", ItemWheelCategory.Stim },
                { "Food", ItemWheelCategory.Food },
                { "Explosive", ItemWheelCategory.Explosive },
                { "MeleeWeapon", ItemWheelCategory.Melee }
            };

        // CategoryWheel 已移到 ItemWheel.Data.CategoryWheel

        // 🆕 单例实例（用于静态方法访问）
        private static ItemWheelSystem _instance;

        [System.NonSerialized]
        private Dictionary<ItemWheelCategory, CategoryWheel> _wheels;

        // 🆕 阶段4：Handler字典（模块化处理不同物品类型）
        [System.NonSerialized]
        private Dictionary<ItemWheelCategory, Handlers.IItemHandler> _handlers;

        [System.NonSerialized]
        private CharacterMainControl _character;

        [System.NonSerialized]
        private Inventory _inventory;

        // 🆕 子弹时间管理器（根据配置启用）
        [System.NonSerialized]
        private BulletTimeManager _bulletTimeManager;

        // 🆕 子弹HUD着色器
        [System.NonSerialized]
        private BulletHUDColorizer _bulletHUDColorizer;

        // 🆕 防止递归事件标志：轮盘拖拽时同步背包，避免触发背包变化事件再次更新轮盘
        private bool _isPerformingSwap = false;

        // 🆕 待定消失的物品（延迟判断机制，避免拖拽时误判为消失）
        private class PendingDisappearance
        {
            public ItemWheelCategory Category;
            public Item Item;
            public int FrameCount;
            public const int MAX_WAIT_FRAMES = 5; // 等待5帧（约0.08秒）
        }
        private List<PendingDisappearance> _pendingDisappearances = new List<PendingDisappearance>();

        public ItemWheelSystem()
        {
            _instance = this;
            _wheels = new Dictionary<ItemWheelCategory, CategoryWheel>();
            LevelManager.OnLevelInitialized += HandleLevelInitialized;

            // 🆕 使用统一的 WheelSpriteLoader 加载自定义格子Sprite
            WheelSpriteLoader.Load();

            // 🆕 阶段4：初始化Handler
            InitializeHandlers();

            // 🆕 初始化子弹时间管理器（根据配置）
            InitializeBulletTime();

            // 🆕 初始化子弹HUD着色器
            _bulletHUDColorizer = new BulletHUDColorizer();
        }

        /// <summary>
        /// 🆕 阶段4：初始化Handler字典
        /// </summary>
        private void InitializeHandlers()
        {
            _handlers = new Dictionary<ItemWheelCategory, Handlers.IItemHandler>();

            // 医疗品、刺激物、食物使用默认Handler
            _handlers[ItemWheelCategory.Medical] = new Handlers.DefaultItemHandler(ItemWheelCategory.Medical);
            _handlers[ItemWheelCategory.Stim] = new Handlers.DefaultItemHandler(ItemWheelCategory.Stim);
            _handlers[ItemWheelCategory.Food] = new Handlers.DefaultItemHandler(ItemWheelCategory.Food);

            // 手雷使用专用Handler
            _handlers[ItemWheelCategory.Explosive] = new Handlers.ExplosiveHandler();

            // 近战使用专用Handler（需要inventory访问）
            _handlers[ItemWheelCategory.Melee] = new Handlers.MeleeHandler(() => _inventory);

            Debug.Log("[ItemWheel] Handlers initialized");
        }

        /// <summary>
        /// 🆕 初始化子弹时间管理器
        /// </summary>
        private void InitializeBulletTime()
        {
            var settings = ModSettingFacade.Settings;

            if (settings.EnableBulletTime)
            {
                _bulletTimeManager = new BulletTimeManager(
                    targetTimeScale: settings.BulletTimeScale,
                    transitionSpeed: settings.BulletTimeTransitionSpeed,
                    adjustAudioPitch: settings.BulletTimeAdjustAudioPitch
                );

                Debug.Log($"[ItemWheel] BulletTime initialized - Scale: {settings.BulletTimeScale}x");
            }
        }

        /// <summary>
        /// 🆕 静态方法：启用子弹时间（供外部系统如AmmoWheel调用）
        /// </summary>
        public static void EnableBulletTime()
        {
            _instance?._bulletTimeManager?.Enable();
        }

        /// <summary>
        /// 🆕 静态方法：禁用子弹时间（供外部系统如AmmoWheel调用）
        /// </summary>
        public static void DisableBulletTime()
        {
            _instance?._bulletTimeManager?.Disable();
        }

        /// <summary>
        /// 🆕 重新初始化子弹时间（配置加载后调用）
        /// </summary>
        public void ReinitializeBulletTime()
        {
            // 清理旧实例
            if (_bulletTimeManager != null)
            {
                _bulletTimeManager.ForceRestore();
                _bulletTimeManager = null;
            }

            // 重新初始化
            InitializeBulletTime();
        }


        /// <summary>
        /// 检查是否有活跃的轮盘
        /// </summary>
        public bool HasActiveWheel
        {
            get
            {
                foreach (var wheel in _wheels.Values)
                {
                    if (wheel.Wheel != null && wheel.Wheel.IsVisible)
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 显示指定类别的轮盘
        /// </summary>
        /// <param name="category">物品类别</param>
        /// <param name="wheelCenter">轮盘中心位置（可选，为null则使用当前鼠标位置）</param>
        /// <returns>是否成功显示</returns>
        public bool ShowWheel(ItemWheelCategory category, Vector2? wheelCenter = null)
        {
            if (!IsCategoryEnabled(category))
            {
                return false;
            }

            var wheel = EnsureWheel(category);

            // 🆕 打开轮盘时不重置选择，也不修改快捷栏（保持官方快捷栏不变）
            if (!RefreshCategorySlots(wheel, resetSelection: false, skipShortcutSync: true))
            {
                Debug.LogWarning($"[轮盘] 刷新失败: {category}");
                return false;
            }

            HideAllWheels();

            if (wheelCenter.HasValue)
            {
                wheel.View?.SetWheelCenterBeforeShow(wheelCenter.Value);
            }

            wheel.Input?.SetPressedState(true);

            if (wheel.LastConfirmedIndex >= 0 && wheel.LastConfirmedIndex < wheel.Slots.Length && wheel.Slots[wheel.LastConfirmedIndex] != null)
            {
                wheel.Wheel.SetSelectedIndex(wheel.LastConfirmedIndex);
            }

            // 🆕 阶段4：轮盘显示前调用Handler
            if (_handlers != null && _handlers.TryGetValue(category, out var handler))
            {
                handler.OnWheelShown(wheel);
            }

            // 🆕 启用子弹时间
            _bulletTimeManager?.Enable();

            // 新一轮显示，重置"本次是否交换"标记
            _sessionSwapped[category] = false;
            wheel.Wheel.Show();
            return true;
        }

        /// <summary>
        /// 隐藏所有轮盘
        /// </summary>
        public void HideAllWheels()
        {
            foreach (var wheel in _wheels.Values)
            {
                wheel.Input?.SetPressedState(false);  // 重置输入状态
                wheel.Wheel?.ManualCancel();
            }

            // 🆕 禁用子弹时间
            _bulletTimeManager?.Disable();

            // 兜底：全局清理任意残留的拖拽状态，防止自投/异常导致的拖拽幽灵与 hover 卡住
            try
            {
                var slots = UnityEngine.Object.FindObjectsOfType<QuickWheel.UI.WheelSlotDisplay>();
                if (slots != null)
                {
                    foreach (var slot in slots)
                    {
                        slot.ForceCleanupDrag();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ItemWheel] HideAllWheels cleanup warning: {ex.Message}");
            }
        }

        /// <summary>
        /// 按键状态管理
        /// </summary>
        private sealed class KeyState
        {
            public bool IsPressed;
            public float HoldTime;
            public bool HasTriggeredWheel;
            public Vector2 PressedMousePosition;  // 按下时的鼠标位置
        }

        private readonly Dictionary<ItemWheelCategory, KeyState> _keyStates = new();
        // 本次显示期间是否发生过交换（会话级，按类别记录）
        private readonly Dictionary<ItemWheelCategory, bool> _sessionSwapped = new();

        private static bool IsCategoryEnabled(ItemWheelCategory category)
        {
            return ModSettingFacade.Settings.IsWheelEnabled(category);
        }

        /// <summary>
        /// 按键按下事件（由ModBehavior调用）
        /// 开始长按计时
        /// </summary>
        /// <param name="category">物品类别</param>
        public void OnKeyPressed(ItemWheelCategory category)
        {
            if (!IsCategoryEnabled(category))
            {
                return;
            }

            if (!_keyStates.TryGetValue(category, out var state))
            {
                state = new KeyState();
                _keyStates[category] = state;
            }

            state.IsPressed = true;
            state.HoldTime = 0f;
            state.HasTriggeredWheel = false;
            state.PressedMousePosition = UnityEngine.Input.mousePosition;  // ⭐ 立即记录鼠标位置

            // 确保轮盘存在（预热）
            EnsureWheel(category);
        }

        /// <summary>
        /// 按键松开事件（由ModBehavior调用）
        /// 根据长按状态决定执行逻辑
        /// </summary>
        /// <param name="category">物品类别</param>
        public void OnKeyReleased(ItemWheelCategory category)
        {
            if (!IsCategoryEnabled(category))
            {
                return;
            }

            if (!_keyStates.TryGetValue(category, out var state))
            {
                return;
            }

            state.IsPressed = false;

            if (state.HasTriggeredWheel)
            {
                // 长按了：确认轮盘选择
                ConfirmWheelSelection(category);
            }
            else
            {
                // 短按：直接使用物品
                // 近战武器：短按不处理，让官方方法生效（在ModBehaviour的Harmony Patch中处理）
                if (category != ItemWheelCategory.Melee)
                {
                    UseShortcutDirect(category);
                }
            }

            // 重置状态
            state.HoldTime = 0f;
            state.HasTriggeredWheel = false;
        }

        /// <summary>
        /// 检查指定类别是否已触发轮盘（用于Harmony Patch判断）
        /// </summary>
        public bool HasTriggeredWheel(ItemWheelCategory category)
        {
            if (!IsCategoryEnabled(category))
            {
                return false;
            }

            if (_keyStates.TryGetValue(category, out var state))
            {
                return state.HasTriggeredWheel;
            }
            return false;
        }

        /// <summary>
        /// 直接使用快捷物品（避免循环调用）
        /// </summary>
        /// <param name="category">物品类别</param>
        private void UseShortcutDirect(ItemWheelCategory category)
        {
            if (!IsCategoryEnabled(category))
            {
                return;
            }

            if (!_wheels.TryGetValue(category, out var wheel))
            {
                return;  // 轮盘还未创建，忽略
            }

            // 短按不应触发重排/重建布局，避免快捷键UI变化
            // 仅在首次未初始化时刷新一次（并且不重置选择，不修改快捷栏）
            if (wheel.Slots == null || wheel.Slots.All(s => s == null))
            {
                if (!RefreshCategorySlots(wheel, resetSelection: false, skipShortcutSync: true))
                {
                    return;
                }
            }

            int index = GetPreferredIndex(wheel);
            if (index < 0 || index >= wheel.Slots.Length)
            {
                return;
            }

            Item item = wheel.Slots[index];
            if (item == null)
            {
                return;
            }

            wheel.LastConfirmedIndex = index;
            wheel.LastSelectedItem = item;  // 🆕 更新选中物品引用
            UseItem(item, category);
        }

        /// <summary>
        /// 鼠标滚轮切换当前显示轮盘的选中槽位
        /// </summary>
        /// <param name="direction">滚轮方向：正数表示向后，负数表示向前</param>
        public void OnWheelScroll(int direction)
        {
            if (direction == 0)
            {
                return;
            }

            CategoryWheel activeWheel = null;
            foreach (var wheel in _wheels.Values)
            {
                if (wheel?.Wheel != null && wheel.Wheel.IsVisible)
                {
                    activeWheel = wheel;
                    break;
                }
            }

            if (activeWheel == null)
            {
                return;
            }

            var quickWheel = activeWheel.Wheel;
            if (quickWheel == null)
            {
                return;
            }

            int slotCount = quickWheel.Config?.SlotCount ?? WheelConfig.SLOT_COUNT;
            if (slotCount <= 0)
            {
                return;
            }

            int currentIndex = quickWheel.GetSelectedIndex();
            if (currentIndex < 0)
            {
                currentIndex = activeWheel.LastConfirmedIndex;
            }
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int attempts = 0;
            int nextIndex = currentIndex;
            while (attempts < slotCount)
            {
                nextIndex = WrapWheelIndex(nextIndex + direction, slotCount);
                attempts++;

                // 跳过中心槽（索引 slotCount - 1）
                if (nextIndex == slotCount - 1)
                {
                    continue;
                }

                if (nextIndex < 0 || nextIndex >= activeWheel.Slots.Length)
                {
                    continue;
                }

                if (activeWheel.Slots[nextIndex] == null)
                {
                    continue;
                }

                quickWheel.SetSelectedIndex(nextIndex);
                OnSelectionChanged(activeWheel, nextIndex);
                return;
            }
        }

        private static int WrapWheelIndex(int index, int slotCount)
        {
            if (slotCount <= 0)
            {
                return 0;
            }

            int normalized = index % slotCount;
            if (normalized < 0)
            {
                normalized += slotCount;
            }
            return normalized;
        }

        /// <summary>
        /// 每帧更新方法（处理长按计时和轮盘逻辑）
        /// </summary>
        public void Update()
        {
            // 🆕 更新子弹时间
            _bulletTimeManager?.Update();

            // 🆕 更新子弹HUD颜色
            _bulletHUDColorizer?.Update();

            // 🆕 处理待定消失的物品
            ProcessPendingDisappearances();

            // 处理长按计时
            HandleLongPressTimers();

            // 更新可见的轮盘（包括鼠标输入）
            if (HasActiveWheel)
            {
                foreach (var wheel in _wheels.Values)
                {
                    if (wheel.Wheel != null && wheel.Wheel.IsVisible)
                    {
                        // Wheel.Update()内部会调用InputHandler.OnUpdate()，不需要重复调用
                        wheel.Wheel.Update();
                    }
                }
            }
        }

        /// <summary>
        /// 处理长按计时逻辑
        /// </summary>
        private void HandleLongPressTimers()
        {
            float deltaTime = Time.unscaledDeltaTime;
            const float longPressThreshold = 0.2f;

            foreach (var kvp in _keyStates)
            {
                var category = kvp.Key;
                var state = kvp.Value;

                if (!IsCategoryEnabled(category))
                {
                    continue;
                }

                if (state.IsPressed && !state.HasTriggeredWheel)
                {
                    state.HoldTime += deltaTime;

                    if (state.HoldTime >= longPressThreshold)
                    {
                        // 达到长按阈值，显示轮盘
                        state.HasTriggeredWheel = true;
                        ShowWheel(category, state.PressedMousePosition);  // ⭐ 传递按下时的鼠标位置
                    }
                }
            }
        }

        /// <summary>
        /// 确认轮盘选择
        /// </summary>
        private void ConfirmWheelSelection(ItemWheelCategory category)
        {
            if (!IsCategoryEnabled(category))
            {
                return;
            }

            if (_wheels.TryGetValue(category, out var wheel))
            {
                // 🆕 关闭轮盘时禁用子弹时间
                _bulletTimeManager?.Disable();

                // 若本次显示期间发生过交换，关闭时不使用物品，直接取消
                if (_sessionSwapped.TryGetValue(category, out bool swapped) && swapped)
                {
                    Debug.Log($"[轮盘] 本次发生过交换，关闭时取消选择: {category}");
                    wheel.Wheel?.ManualCancel();
                }
                else
                {
                    // 🆕 修复：松开键盘时应该使用hover的物品，不是ManualConfirm
                    // ManualConfirm只更新选中，不使用物品（那是点击的行为）
                    wheel.Wheel?.Hide(executeSelection: true);
                }
            }
        }

        /// <summary>
        /// 释放资源方法
        /// 清理所有轮盘实例和字典数据
        /// </summary>
        public void Dispose()
        {
            // 🆕 强制恢复正常时间（防止退出游戏时时间被锁定）
            _bulletTimeManager?.ForceRestore();

            // 🆕 释放子弹HUD着色器
            _bulletHUDColorizer?.Dispose();

            // 🆕 取消背包监听
            if (_inventory != null)
            {
                _inventory.onContentChanged -= OnInventoryContentChanged;
            }

            foreach (CategoryWheel categoryWheel in _wheels.Values)
            {
                categoryWheel.Wheel?.Dispose();
                // Input处理器由Wheel管理，不需要单独释放
            }

            WheelInputGuard.Reset();
            _wheels.Clear();

            // 清除单例引用
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 关卡初始化事件处理
        /// 绑定主角色并重置轮盘状态
        /// </summary>
        private void HandleLevelInitialized()
        {
            var mainCharacter = CharacterMainControl.Main;
            if (mainCharacter == null)
            {
                Debug.LogWarning("[ItemWheel] Main character not available during level initialization.");
                return;
            }

            BindCharacter(mainCharacter);
            // 不再需要ResetWheelStates，因为没有按键状态管理了
        }

        /// <summary>
        /// 绑定角色数据
        /// 保存角色引用和物品栏引用
        /// </summary>
        /// <param name="character">要绑定的角色</param>
        public void BindCharacter(CharacterMainControl character)
        {
            // 取消旧的背包监听
            if (_inventory != null)
            {
                _inventory.onContentChanged -= OnInventoryContentChanged;
            }

            _character = character;
            _inventory = character?.CharacterItem?.Inventory;

            if (_inventory != null)
            {
                // 🆕 订阅背包内容变化事件
                _inventory.onContentChanged += OnInventoryContentChanged;

                // 🆕 不再在这里初始化轮盘，交由 ModBehaviour 的 Hook 在 ItemShortcut.Load() 完成后调用
            }
        }

        /// <summary>
        /// 🆕 游戏启动时初始化所有轮盘，触发首次加载逻辑
        /// </summary>
        internal void InitializeAllWheelsOnStart()
        {
            Debug.Log("[ItemWheel] 🎮 游戏启动，初始化所有轮盘...");

            // 遍历所有类别（不包括近战）
            var categories = new[]
            {
                ItemWheelCategory.Medical,
                ItemWheelCategory.Stim,
                ItemWheelCategory.Food,
                ItemWheelCategory.Explosive
            };

            foreach (var category in categories)
            {
                // 检查配置是否启用该轮盘
                if (!ModSettingFacade.Settings.IsWheelEnabled(category))
                {
                    Debug.Log($"[ItemWheel] 跳过禁用的轮盘: {category}");
                    continue;
                }

                // 确保轮盘存在
                var wheel = EnsureWheel(category);

                // 刷新轮盘，触发 IsFirstLoad 逻辑
                RefreshCategorySlots(wheel, resetSelection: false);

                Debug.Log($"[ItemWheel] ✅ 初始化轮盘: {category}, 选中索引={wheel.LastConfirmedIndex}");
            }
        }

        /// <summary>
        /// 🆕 背包内容变化事件处理器
        /// 当背包中物品位置变化时，刷新轮盘映射
        /// 🆕 优化：只刷新受影响的类别，保持其他类别选中状态
        /// 🆕 手雷特殊处理：在 ContentChanged 中处理堆叠逻辑
        /// </summary>
        private void OnInventoryContentChanged(Inventory inventory, int changedSlot)
        {
            // 🆕 在交换过程中跳过处理，避免递归
            if (_isPerformingSwap)
            {
                Debug.Log($"[轮盘] ⚠️ 背包变化(slot={changedSlot})被跳过，正在执行交换");
                return;
            }

            // 🆕 智能刷新：分析变化的物品属于哪个轮盘类别
            Item changedItem = (inventory?.Content != null && changedSlot >= 0 && changedSlot < inventory.Content.Count)
                ? inventory.Content[changedSlot]
                : null;

            if (changedItem != null)
            {
                // 检查该物品属于哪个轮盘类别
                ItemWheelCategory? affectedCategory = null;
                string tagName = null;
                var tags = changedItem.Tags; // Tags is TagCollection type
                if (tags != null)
                {
                    foreach (var tag in tags)
                    {
                        if (tag != null && !string.IsNullOrEmpty(tag.name))
                        {
                            tagName = tag.name;
                            Debug.Log($"[轮盘] 🔍 检查标签: '{tagName}' for item {changedItem.DisplayName}");
                            if (TagMappings.TryGetValue(tag.name, out ItemWheelCategory category))
                            {
                                affectedCategory = category;
                                Debug.Log($"[轮盘] ✅ 找到匹配! 标签 '{tag.name}' -> 类别 {category}");
                                break;
                            }
                        }
                    }
                }

                if (affectedCategory.HasValue)
                {
                    Debug.Log($"[轮盘] 🎯 背包变化: slot={changedSlot}, 物品: {changedItem.DisplayName}, 类别={affectedCategory.Value}");

                    // 🆕 检查该轮盘是否被禁用，如果禁用则跳过处理
                    if (!ModSettingFacade.Settings.IsWheelEnabled(affectedCategory.Value))
                    {
                        Debug.Log($"[轮盘] ⏭️ 轮盘已禁用，跳过处理: {affectedCategory.Value}");
                        return;
                    }

                    if (_wheels.TryGetValue(affectedCategory.Value, out CategoryWheel affectedWheel))
                    {
                        // 🆕 修复：从 Slots 获取选中项可能不准确（背包变化后 Slots 引用可能错位）
                        // 使用 LastSelectedItem 直接保存的引用
                        Item previouslySelectedItem = affectedWheel.LastSelectedItem;

                        // 🆕 添加边界检查，防止数组越界
                        string slotItemName = "无效索引";
                        if (affectedWheel.LastConfirmedIndex >= 0 &&
                            affectedWheel.LastConfirmedIndex < affectedWheel.Slots.Length)
                        {
                            slotItemName = affectedWheel.Slots[affectedWheel.LastConfirmedIndex]?.DisplayName ?? "空";
                        }

                        Debug.Log($"[轮盘] 🔍 背包变化前选中: LastConfirmedIndex={affectedWheel.LastConfirmedIndex}, " +
                                  $"Slots[{affectedWheel.LastConfirmedIndex}]={slotItemName}, " +
                                  $"LastSelectedItem={previouslySelectedItem?.DisplayName}");

                        // 🆕 背包变化时：先刷新槽位（不设置快捷栏），等恢复选中项后再同步快捷栏
                        RefreshCategorySlots(affectedWheel, resetSelection: false, skipShortcutSync: true);

                        // 尝试恢复之前的选中项（如果该物品仍然存在）
                        if (previouslySelectedItem != null)
                        {
                            int restoredIndex = FindItemIndexInSlots(affectedWheel.Slots, previouslySelectedItem);
                            if (restoredIndex >= 0)
                            {
                                // 物品找到了，恢复选中
                                affectedWheel.LastConfirmedIndex = restoredIndex;
                                affectedWheel.LastSelectedItem = previouslySelectedItem;  // 保持引用
                                Debug.Log($"[轮盘] ✅ 恢复选中项: {previouslySelectedItem.DisplayName}, 位置: {restoredIndex}");

                                // 恢复选中后，同步快捷栏
                                if (affectedWheel.Category != ItemWheelCategory.Melee)
                                {
                                    var shortcutIndex = (int)affectedWheel.Category;
                                    Duckov.ItemShortcut.Set(shortcutIndex, previouslySelectedItem);
                                    Debug.Log($"[轮盘] 🔄 重新同步快捷栏: 类别={affectedWheel.Category}, 物品={previouslySelectedItem.DisplayName}");
                                }
                            }
                            else
                            {
                                // 物品找不到了，检查是暂时找不到还是真的离开了
                                bool stillInValidLocation = IsItemStillInValidLocation(previouslySelectedItem);

                                if (stillInValidLocation)
                                {
                                    // 物品还在合法位置（主背包/容器/宠物背包），但暂时找不到
                                    // 可能正在拖拽或整理，保持 LastSelectedItem 不变，不更新快捷键
                                    Debug.Log($"[轮盘] ⏸️ 物品暂时找不到（正在移动）: {previouslySelectedItem.DisplayName}，保持选中不变");
                                }
                                else
                                {
                                    // 物品离开了合法位置，但可能是正在被拖拽
                                    // 🆕 添加到待定列表，延迟几帧再判断是否真的消失
                                    var existing = _pendingDisappearances.Find(p =>
                                        p.Category == affectedWheel.Category &&
                                        ReferenceEquals(p.Item, previouslySelectedItem));

                                    if (existing == null)
                                    {
                                        _pendingDisappearances.Add(new PendingDisappearance
                                        {
                                            Category = affectedWheel.Category,
                                            Item = previouslySelectedItem,
                                            FrameCount = 0
                                        });
                                        Debug.Log($"[轮盘] ⏳ 物品标记为待定消失: {previouslySelectedItem.DisplayName}，等待{PendingDisappearance.MAX_WAIT_FRAMES}帧确认");
                                    }
                                }
                            }
                        }
                    }
                    return; // 只处理一个类别，避免多次刷新
                }
                else
                {
                    // 如果物品类别不在ItemWheel管理范围内（如子弹），跳过刷新
                    Debug.Log($"[轮盘] ⏭️ 物品类别不在ItemWheel管理范围内，跳过刷新: {changedItem?.DisplayName}");
                    return;
                }
            }
            else
            {
                Debug.Log($"[轮盘] 📦 变化物品为null (slot={changedSlot}可能是被清空了)，将刷新所有类别");
            }

            // 如果物品为null（可能是被清空），刷新所有类别但不重置选择，不修改快捷栏
            Debug.Log($"[轮盘] ⚠️ 物品为null，刷新所有类别但保持选中");
            foreach (var kvp in _wheels)
            {
                RefreshCategorySlots(kvp.Value, resetSelection: false, skipShortcutSync: true);
            }
        }

        /// <summary>
        /// 🆕 检查物品是否还在合法的搜索范围内（根据设置）
        /// 用于判断物品是暂时找不到（拖拽/整理中）还是真的离开了
        /// </summary>
        private bool IsItemStillInValidLocation(Item item)
        {
            if (item == null) return false;

            var settings = ModSettingFacade.Settings;

            // 1. 检查是否在主背包中
            if (item.InInventory == _inventory)
            {
                return true;
            }

            // 2. 检查是否在主背包的容器中（如果设置允许）
            if (settings.SearchInSlots && _inventory != null && _inventory.Content != null)
            {
                foreach (var containerItem in _inventory.Content)
                {
                    if (containerItem != null && containerItem.Inventory != null)
                    {
                        if (item.InInventory == containerItem.Inventory)
                        {
                            return true;
                        }
                    }
                }
            }

            // 3. 检查是否在宠物背包中（如果设置允许）
            if (settings.SearchInPetInventory)
            {
                var petInventory = PetProxy.PetInventory;
                if (petInventory != null)
                {
                    // 检查是否在宠物背包顶层
                    if (item.InInventory == petInventory)
                    {
                        return true;
                    }

                    // 检查是否在宠物背包的容器中（如果设置允许）
                    if (settings.SearchInSlots && petInventory.Content != null)
                    {
                        foreach (var containerItem in petInventory.Content)
                        {
                            if (containerItem != null && containerItem.Inventory != null)
                            {
                                if (item.InInventory == containerItem.Inventory)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 🆕 处理待定消失的物品（延迟判断机制）
        /// 每帧检查待定列表，如果物品重新出现就恢复选中，如果超时就切换到其他物品
        /// </summary>
        private void ProcessPendingDisappearances()
        {
            if (_pendingDisappearances.Count == 0) return;

            for (int i = _pendingDisappearances.Count - 1; i >= 0; i--)
            {
                var pending = _pendingDisappearances[i];
                pending.FrameCount++;

                // 尝试在轮盘中找到该物品
                if (_wheels.TryGetValue(pending.Category, out var wheel))
                {
                    int foundIndex = FindItemIndexInSlots(wheel.Slots, pending.Item);

                    if (foundIndex >= 0)
                    {
                        // 物品重新出现了！恢复选中
                        wheel.LastConfirmedIndex = foundIndex;
                        wheel.LastSelectedItem = pending.Item;
                        Debug.Log($"[轮盘] ✅ 待定物品重新出现: {pending.Item.DisplayName}, 位置: {foundIndex}");

                        // 同步快捷栏
                        if (wheel.Category != ItemWheelCategory.Melee)
                        {
                            var shortcutIndex = (int)wheel.Category;
                            Duckov.ItemShortcut.Set(shortcutIndex, pending.Item);
                            Debug.Log($"[轮盘] 🔄 重新同步快捷栏: 类别={wheel.Category}, 物品={pending.Item.DisplayName}");
                        }

                        // 移除待定项
                        _pendingDisappearances.RemoveAt(i);
                    }
                    else if (pending.FrameCount >= PendingDisappearance.MAX_WAIT_FRAMES)
                    {
                        // 超时了，确认物品真的消失了
                        wheel.LastConfirmedIndex = GetFirstAvailableItemIndex(wheel);
                        if (wheel.LastConfirmedIndex >= 0)
                        {
                            wheel.LastSelectedItem = wheel.Slots[wheel.LastConfirmedIndex];
                            Debug.Log($"[轮盘] ⚠️ 确认物品消失: {pending.Item.DisplayName}，自动选择下一个: {wheel.LastSelectedItem?.DisplayName}");

                            // 同步快捷栏
                            if (wheel.Category != ItemWheelCategory.Melee)
                            {
                                var shortcutIndex = (int)wheel.Category;
                                Duckov.ItemShortcut.Set(shortcutIndex, wheel.LastSelectedItem);
                                Debug.Log($"[轮盘] 🔄 重新同步快捷栏: 类别={wheel.Category}, 物品={wheel.LastSelectedItem?.DisplayName}");
                            }
                        }
                        else
                        {
                            // 没有可用物品了
                            wheel.LastSelectedItem = null;
                            Debug.Log($"[轮盘] ❌ 没有可用物品了");
                        }

                        // 移除待定项
                        _pendingDisappearances.RemoveAt(i);
                    }
                    // 否则继续等待
                }
                else
                {
                    // 类别不存在了，移除待定项
                    _pendingDisappearances.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 手动触发 Inventory 的 onContentChanged 事件（使用反射）
        /// </summary>
        private void TriggerInventoryContentChanged(Inventory inventory, int position)
        {
            try
            {
                // 使用反射获取事件字段
                var eventField = typeof(Inventory).GetField("onContentChanged",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);

                if (eventField != null)
                {
                    var eventDelegate = eventField.GetValue(inventory) as System.Action<Inventory, int>;
                    eventDelegate?.Invoke(inventory, position);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[轮盘] 触发 onContentChanged 事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 🆕 在轮盘格子中查找物品的索引（优先引用匹配，备用 TypeID 匹配）
        /// </summary>
        private static int FindItemIndexInSlots(Item[] slots, Item targetItem)
        {
            if (slots == null || targetItem == null) return -1;

            // 第一遍：引用相等性匹配（最精确）
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == targetItem)
                {
                    Debug.Log($"[轮盘] 🔍 通过引用找到物品: {targetItem.DisplayName} @ 位置{i}");
                    return i;
                }
            }

            // 第二遍：TypeID 匹配（备用方案，处理引用改变的情况）
            int targetTypeId = targetItem.TypeID;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].TypeID == targetTypeId)
                {
                    Debug.Log($"[轮盘] ⚠️ 通过TypeID找到物品: {targetItem.DisplayName} (TypeID={targetTypeId}) @ 位置{i}");
                    return i;
                }
            }

            Debug.Log($"[轮盘] ❌ 未找到物品: {targetItem.DisplayName} (TypeID={targetTypeId})");
            return -1;
        }

        // 删除GetCategoryForKey方法，未使用

        /// <summary>
        /// 确保轮盘存在（延迟创建模式）
        /// 如果轮盘不存在则创建新的轮盘实例
        /// </summary>
        /// <param name="category">物品类别</param>
        /// <returns>类别轮盘实例</returns>
        private CategoryWheel EnsureWheel(ItemWheelCategory category)
        {
            if (_wheels.TryGetValue(category, out CategoryWheel existing))
            {
                return existing;
            }

            var context = new CategoryWheel
            {
                Category = category,
                Slots = new Item[WheelConfig.SLOT_COUNT],
                LastConfirmedIndex = -1
                // ShowPosition 已删除，不再需要
            };

            // ✅ 使用简化的MouseWheelInput，只处理鼠标移动
            var input = new QuickWheel.Input.MouseWheelInput();
            var view = new DefaultWheelView<Item>();  // ⭐ 创建View实例

            // 🆕 使用上下文感知的适配器，能够访问堆叠信息
            var adapter = new ItemWheelContextualAdapter(context);

            Wheel<Item> wheel = new WheelBuilder<Item>()
                .WithConfig(cfg =>
                {
                    cfg.EnablePersistence = false;
                    cfg.GridCellSize = 90f;  // 格子大小（像素）
                    cfg.GridSpacing = 12f;   // 格子间距（像素）
                    cfg.DeadZoneRadius = 40f; // 死区半径（像素）

                    // 🆕 使用 WheelSpriteLoader 加载的自定义格子Sprite
                    cfg.SlotNormalSprite = WheelSpriteLoader.SlotNormal;
                    cfg.SlotHoverSprite = WheelSpriteLoader.SlotHover;
                    cfg.SlotSelectedSprite = WheelSpriteLoader.SlotSelected;

                    // 🆕 阶段3：拖拽验证回调 - 只允许主背包顶层单物品拖拽
                    cfg.CanDragSlot = (slotIndex) =>
                    {
                        return CanDragSlotImpl(context, slotIndex);
                    };
                })
                .WithAdapter(adapter)

                .WithView(view)  // ? ?????View??

                .WithInput(input)  // ? ?????????????

                .WithSelectionStrategy(new GridSelectionStrategy())

                .OnItemSelected((index, item) => OnItemSelected(context, index, item))

                .OnWheelShown(WheelInputGuard.OnWheelShown)

                .OnWheelHidden(index =>

                {

                    WheelInputGuard.OnWheelHidden();

                    OnWheelHidden(context, index);

                })

                .Build();

            context.Wheel = wheel;
            context.Input = input;  // ✅ 保存输入处理器引用
            context.View = view;    // ⭐ 保存View引用
            wheel.SetSlots(context.Slots);

            // 🆕 订阅槽位交换事件：当玩家在轮盘上拖拽物品时，同步到背包
            wheel.EventBus.OnSlotsSwapped += (fromIndex, toIndex) =>
            {
                // 标记本次显示期间发生过交换，用于关闭时防误触
                _sessionSwapped[context.Category] = true;
                OnWheelSlotsSwapped(context, fromIndex, toIndex);
            };

            // 🆕 订阅选中改变事件：直接订阅 Wheel 的事件（绕过 EventBus 的事件锁）
            wheel.OnSelectionChanged += (selectedIndex) =>
            {
                OnSelectionChanged(context, selectedIndex);
            };

            _wheels[category] = context;
            return context;
        }

        // 删除GetTriggerKeyForCategory方法，不再使用KeyCode

        private bool RefreshCategorySlots(CategoryWheel wheel, bool resetSelection = true, bool skipShortcutSync = false)
        {
            if (_isPerformingSwap)
            {
                return true;
            }

            if (_inventory == null)
            {
                return false;
            }

            List<CollectedItemInfo> collected = CollectItemsForCategory(wheel.Category);

            if (collected.Count == 0)
            {
                wheel.Slots = new Item[WheelConfig.SLOT_COUNT];
                wheel.Wheel.SetSlots(wheel.Slots);
                System.Array.Fill(wheel.IsFromSlot, false);  // 重置来源标记
                return false;
            }

            Item[] slotBuffer = new Item[WheelConfig.SLOT_COUNT];

            // 🗑️ 移除历史记录功能：轮盘布局完全由背包物品位置决定，无需持久化
            CreateDefaultMapping(wheel, collected, slotBuffer);

            // 🆕 阶段3：填充 DisplayedItems，用于准确判断物品来源
            // 建立 slotBuffer 到 collected 的映射
            wheel.DisplayedItems.Clear();
            for (int i = 0; i < slotBuffer.Length; i++)
            {
                if (slotBuffer[i] != null)
                {
                    // 在 collected 中查找对应的 CollectedItemInfo
                    var matchInfo = collected.Find(info => info.Item == slotBuffer[i]);
                    if (matchInfo.Item != null)
                    {
                        wheel.DisplayedItems.Add(matchInfo);
                    }
                }
                else
                {
                    // 空槽位，添加空占位符（保持索引对应）
                    wheel.DisplayedItems.Add(default(CollectedItemInfo));
                }
            }

            // 🆕 关键点：必须在 SetSlots 之前填充 ItemInfoMap！
            // 因为 SetSlots 会触发 WheelUIManager 创建显示，立即调用适配器
            // 使用 BackpackIndex 作为键（唯一），避免 Item 引用不匹配问题
            wheel.ItemInfoMap.Clear();
            foreach (var itemInfo in collected)
            {
                if (itemInfo.Item != null)
                {
                    // 🆕 使用 BackpackIndex 作为键（唯一标识）
                    wheel.ItemInfoMap[itemInfo.BackpackIndex] = itemInfo;
                    Debug.Log($"[ItemWheel] 📦 Stored to ItemInfoMap: {itemInfo.Item.DisplayName}, BackpackIndex={itemInfo.BackpackIndex}, StackCount={itemInfo.StackCount}");
                }
            }

            wheel.Slots = slotBuffer;
            wheel.Wheel.SetSlots(slotBuffer);

            // 近战：预先设置默认选中为当前装备的近战（ShowWheel 场景下将避免后续被覆盖）
            TrySetMeleeDefaultSelection(wheel, slotBuffer);

            // 根据 resetSelection 参数决定是否重置选择
            if (resetSelection)
            {
                // 背包变化时：选择第一个可用物品（支持容器和宠物物品）
                wheel.LastConfirmedIndex = GetFirstAvailableItemIndex(wheel);
                if (wheel.LastConfirmedIndex >= 0)
                {
                    wheel.LastSelectedItem = wheel.Slots[wheel.LastConfirmedIndex];
                }
            }
            else
            {
                // 🆕 skipShortcutSync=true 时（背包变化场景），跳过索引检查
                // 让 OnInventoryContentChanged 的恢复逻辑来处理选中状态
                if (!skipShortcutSync)
                {
                    // 只是打开轮盘时：如果之前的选择还存在就保持，否则选第一个可用物品
                    if (wheel.LastConfirmedIndex < 0 || wheel.LastConfirmedIndex >= slotBuffer.Length || slotBuffer[wheel.LastConfirmedIndex] == null)
                    {
                        wheel.LastConfirmedIndex = GetFirstAvailableItemIndex(wheel);
                        if (wheel.LastConfirmedIndex >= 0)
                        {
                            wheel.LastSelectedItem = wheel.Slots[wheel.LastConfirmedIndex];
                        }
                    }
                }
                // 🆕 阶段3：不再重置插槽物品的选中，支持容器和宠物物品保持选中

                // 🆕 首次加载：从官方快捷栏同步选中
                if (wheel.IsFirstLoad && wheel.Category != ItemWheelCategory.Melee)
                {
                    wheel.IsFirstLoad = false;  // 🆕 标记为已加载

                    var shortcutIndex = (int)wheel.Category;
                    Item officialSelectedItem = Duckov.ItemShortcut.Get(shortcutIndex);

                    bool syncSuccess = false;

                    if (officialSelectedItem != null)
                    {
                        Debug.Log($"[ItemWheel] 🔄 首次加载，从官方快捷栏同步: 类别={wheel.Category}, 物品={officialSelectedItem.DisplayName}");

                        // 在轮盘中查找该物品
                        int officialIndex = FindItemIndexInSlots(wheel.Slots, officialSelectedItem);
                        if (officialIndex >= 0)
                        {
                            wheel.LastConfirmedIndex = officialIndex;
                            wheel.LastSelectedItem = wheel.Slots[officialIndex];  // 🆕 同步选中物品引用
                            syncSuccess = true;
                            Debug.Log($"[ItemWheel] ✅ 同步成功: 位置={officialIndex}, 物品={wheel.Slots[officialIndex]?.DisplayName}");
                        }
                        else
                        {
                            Debug.LogWarning($"[ItemWheel] ⚠️ 官方快捷栏物品不在当前轮盘中: {officialSelectedItem.DisplayName}");

                            wheel.LastConfirmedIndex = GetFirstAvailableItemIndex(wheel);
                            if (wheel.LastConfirmedIndex >= 0)
                            {
                                wheel.LastSelectedItem = wheel.Slots[wheel.LastConfirmedIndex];
                                Debug.LogWarning($"[ItemWheel] ⚠️ 设置第一个物品: {wheel.LastSelectedItem.DisplayName}");
                            }
                        }
                    }
                    else
                    {
                        Debug.Log($"[ItemWheel] 官方快捷栏为空，类别={wheel.Category}");
                        wheel.LastConfirmedIndex = GetFirstAvailableItemIndex(wheel);
                        if (wheel.LastConfirmedIndex >= 0)
                        {
                            wheel.LastSelectedItem = wheel.Slots[wheel.LastConfirmedIndex];
                            Debug.LogWarning($"[ItemWheel] ⚠️ 设置第一个物品: {wheel.LastSelectedItem.DisplayName}");
                        }

                    }
                    
                    Debug.Log($"{syncSuccess}， {wheel.LastConfirmedIndex}");

                    // 🆕 阶段3：后备方案 - 如果同步失败，默认选择第一个可用物品，并更新快捷栏
                    if (!syncSuccess && wheel.LastConfirmedIndex < 0)
                    {
                        wheel.LastConfirmedIndex = GetFirstAvailableItemIndex(wheel);
                        if (wheel.LastConfirmedIndex >= 0)
                        {
                            wheel.LastSelectedItem = wheel.Slots[wheel.LastConfirmedIndex];

                            // 🆕 更新快捷栏（官方快捷栏物品不在轮盘中，用第一个可用物品替换）
                            if (!skipShortcutSync)
                            {
                                var newItem = wheel.Slots[wheel.LastConfirmedIndex];
                                Duckov.ItemShortcut.Set(shortcutIndex, newItem);
                                Debug.Log($"[ItemWheel] 使用后备方案，选中第一个可用物品: 位置={wheel.LastConfirmedIndex}, 物品={newItem?.DisplayName}，已更新快捷栏");
                            }
                            else
                            {
                                Debug.Log($"[ItemWheel] 使用后备方案，选中第一个可用物品: 位置={wheel.LastConfirmedIndex}");
                            }
                        }
                    }
                }
            }

            // 更新快捷栏UI（近战不更新官方快捷栏，避免错位）
            // 🆕 skipShortcutSync: 背包变化时跳过快捷栏同步，等恢复选中项后再同步
            if (!skipShortcutSync && wheel.LastConfirmedIndex >= 0 && wheel.Category != ItemWheelCategory.Melee)
            {
                // 🆕 阶段3：支持容器和宠物物品选中到快捷栏
                var shortcutIndex = (int)wheel.Category;
                var selectedItem = slotBuffer[wheel.LastConfirmedIndex];
                Duckov.ItemShortcut.Set(shortcutIndex, selectedItem);

                // 🆕 阶段3：准确判断物品来源
                string source = GetItemSourceDescription(selectedItem, wheel, wheel.LastConfirmedIndex);
                Debug.Log($"[ItemWheel] 设置快捷栏: 类别={wheel.Category}, 物品={selectedItem?.DisplayName}, 来源={source}");
            }
            else if (skipShortcutSync)
            {
                Debug.Log($"[ItemWheel] ⏭️ 跳过快捷栏同步（等待恢复选中项）");
            }

            return true;
        }

        /// <summary>
        /// 🆕 阶段3：获取第一个可用的物品索引（支持容器和宠物物品）
        /// </summary>
        private static int GetFirstAvailableItemIndex(CategoryWheel wheel)
        {
            if (wheel == null || wheel.Slots == null)
            {
                return -1;
            }

            for (int i = 0; i < wheel.Slots.Length; i++)
            {
                if (i == 8)
                {
                    continue;  // 跳过中心位置
                }

                if (wheel.Slots[i] != null)
                {
                    // 阶段3：支持所有来源的物品（背包、容器、宠物）
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 获取物品来源描述（使用 DisplayedItems 精确判断）
        /// </summary>
        private static string GetItemSourceDescription(Item item, CategoryWheel wheel, int wheelIndex)
        {
            if (item == null)
                return "未知";

            // 直接从 DisplayedItems 获取完整信息
            if (wheel.DisplayedItems != null && wheelIndex >= 0 && wheelIndex < wheel.DisplayedItems.Count)
            {
                var itemInfo = wheel.DisplayedItems[wheelIndex];
                if (itemInfo.Item == item)
                {
                    // 使用 CollectedItemInfo 的便捷属性判断来源
                    if (itemInfo.IsFromSlot)
                        return itemInfo.IsFromPet ? "宠物背包容器" : "主背包容器";
                    else
                        return itemInfo.IsFromPet ? "宠物背包" : "主背包";
                }
            }

            // 回退：无法确定来源
            return "未知";
        }

        /// <summary>
        /// （已废弃）获取第一个可用物品索引（包括插槽物品）
        /// 保留用于兼容性，但不再使用
        /// </summary>
        private static int GetFirstAvailableIndex(Item[] slots)
        {
            if (slots == null)
            {
                return -1;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (i == 8)
                {
                    continue;
                }

                if (slots[i] != null)
                {
                    return i;
                }
            }

            return -1;
        }

        private int GetPreferredIndex(CategoryWheel wheel)
        {
            if (wheel.LastConfirmedIndex >= 0 &&
                wheel.LastConfirmedIndex < wheel.Slots.Length &&
                wheel.Slots[wheel.LastConfirmedIndex] != null)
            {
                // 🆕 阶段3：支持容器和宠物物品，不再跳过
                // 手雷特殊处理：需要从 AllLocations 中验证是否还有可用物品
                if (wheel.Category == ItemWheelCategory.Explosive)
                {
                    Item selectedItem = wheel.Slots[wheel.LastConfirmedIndex];
                    if (selectedItem != null && wheel.ItemInfoMap != null)
                    {
                        // 使用 TypeID 查找匹配的堆叠
                        bool foundInfo = false;
                        CollectedItemInfo itemInfo = default(CollectedItemInfo);
                        string selectedTypeId = selectedItem.TypeID.ToString();

                        foreach (var kvp in wheel.ItemInfoMap)
                        {
                            if (kvp.Value.Item != null && kvp.Value.Item.TypeID.ToString() == selectedTypeId)
                            {
                                itemInfo = kvp.Value;
                                foundInfo = true;
                                break;
                            }
                        }

                        if (foundInfo && itemInfo.AllLocations != null && itemInfo.AllLocations.Count > 0)
                        {
                            // 返回第一个可用物品的背包位置映射到轮盘索引
                            // 对于手雷堆叠，轮盘上只有一个格子代表所有同类手雷
                            return wheel.LastConfirmedIndex;
                        }
                    }
                }

                return wheel.LastConfirmedIndex;
            }

            // 选择第一个可用物品（支持容器和宠物物品）
            return GetFirstAvailableItemIndex(wheel);
        }

        // 删除UpdateHover方法，QuickWheel自己管理hover状态

        private void OnItemSelected(CategoryWheel wheel, int index, Item item)
        {
            if (wheel == null)
            {
                return;
            }

            // 🆕 松开快捷键：只使用物品，不改变下次打开的默认选中
            // LastConfirmedIndex 只在点击时通过 OnSelectionChanged 更新
            if (item != null)
            {
                // 🆕 在使用物品前，记录物品的原始位置（修复官方Bug：物品从宠物背包/容器回到玩家背包）
                Patches.CA_UseItem_Patch.RecordItemLocation(item);

                UseItem(item, wheel.Category);

                // 🆕 阶段4：通知Handler物品被选中
                if (_handlers != null && _handlers.TryGetValue(wheel.Category, out var handler))
                {
                    handler.OnItemSelected(item, index, wheel);
                }
            }
        }

        private void OnWheelHidden(CategoryWheel wheel, int index)
        {
            // 不再需要_activeWheel字段，轮盘状态由各自的Wheel管理
        }

        /// <summary>
        /// 🆕 处理选中索引改变事件：更新快捷栏UI（不使用物品）
        /// 参考 backpack_quickwheel 的 ChangeSelection 模式
        /// </summary>
        private void OnSelectionChanged(CategoryWheel wheel, int selectedIndex)
        {
            if (wheel == null) return;

            if (selectedIndex >= 0 && selectedIndex < wheel.Slots.Length && wheel.Slots[selectedIndex] != null)
            {
                // 更新选中索引（支持所有来源的物品）
                wheel.LastConfirmedIndex = selectedIndex;

                // 🆕 保存选中物品的引用（用于背包变化后准确恢复）
                wheel.LastSelectedItem = wheel.Slots[selectedIndex];

                // 同步官方快捷栏（近战不更新官方快捷栏）
                if (wheel.Category != ItemWheelCategory.Melee)
                {
                    var shortcutIndex = (int)wheel.Category;
                    Duckov.ItemShortcut.Set(shortcutIndex, wheel.Slots[selectedIndex]);
                }

                // 准确判断物品来源并记录日志
                string source = GetItemSourceDescription(wheel.Slots[selectedIndex], wheel, selectedIndex);
                Debug.Log($"[轮盘] {wheel.Category} 点击选中: 位置{selectedIndex} {wheel.Slots[selectedIndex].DisplayName} (来源={source})");

                // 🆕 阶段4：近战hover/选中即刻装备（使用Handler）
                if (wheel.Category == ItemWheelCategory.Melee)
                {
                    try
                    {
                        var character = CharacterMainControl.Main ?? _character;
                        var item = wheel.Slots[selectedIndex];
                        if (character != null && item != null)
                        {
                            // 使用Handler处理近战装备
                            if (_handlers != null && _handlers.TryGetValue(ItemWheelCategory.Melee, out var handler))
                            {
                                handler.UseItem(item, character, wheel);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ItemWheel] 近战装备失败: {ex.Message}");
                    }
                }
            }
        }

        // 近战：将物品插入近战槽，并持有到手上；若槽内有旧物，回收到背包
        private void EquipMeleeItem(Item item, CharacterMainControl character)
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
                    EquipItemToHand(item, character);
                    return;
                }

                // 已在槽且已持有则不重复
                if (meleeSlot.Content == item && character.CurrentHoldItemAgent != null && character.CurrentHoldItemAgent.Item == item)
                {
                    return;
                }

                // 插入近战槽（自动处理从背包/其他槽脱离），取出旧物
                Item unplugged;
                bool plugged = meleeSlot.Plug(item, out unplugged);
                if (!plugged)
                {
                    // 插入失败：兜底仅持有
                    EquipItemToHand(item, character);
                    return;
                }

                // 旧物回收至背包
                if (unplugged != null)
                {
                    try { _inventory?.AddItem(unplugged); } catch { }
                }

                // 切换持有
                character.ChangeHoldItem(item);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ItemWheel] EquipMeleeItem 异常: {ex.Message}");
                try { character.ChangeHoldItem(item); } catch { }
            }
        }

        // 近战：设置默认选中为当前装备的近战（若存在且在候选中）
        private void TrySetMeleeDefaultSelection(CategoryWheel wheel, Item[] slotBuffer)
        {
            if (wheel == null || wheel.Category != ItemWheelCategory.Melee || slotBuffer == null)
            {
                return;
            }

            try
            {
                var character = CharacterMainControl.Main ?? _character;
                Item equipped = null;
                if (character?.CurrentHoldItemAgent?.Item != null && MatchesCategoryStatic(character.CurrentHoldItemAgent.Item, ItemWheelCategory.Melee))
                {
                    equipped = character.CurrentHoldItemAgent.Item;
                }
                else
                {
                    var meleeSlot = character?.MeleeWeaponSlot();
                    equipped = meleeSlot != null ? meleeSlot.Content : null;
                }

                if (equipped != null)
                {
                    int idx = Array.IndexOf(slotBuffer, equipped);
                    if (idx >= 0)
                    {
                        wheel.LastConfirmedIndex = idx;
                        wheel.LastSelectedItem = equipped;  // 🆕 更新选中物品引用
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ItemWheel] TrySetMeleeDefaultSelection 失败: {ex.Message}");
            }
        }

        // CollectedItemInfo 已移到 ItemWheel.Data.CollectedItemInfo

        /// <summary>
        /// 从物品栏收集指定类别的所有物品
        /// 🆕 使用 ItemCollector 统一处理，支持多背包、容器、堆叠等
        /// </summary>
        /// <param name="category">要收集的物品类别</param>
        /// <returns>收集到的所有物品列表（无数量限制）</returns>
        private List<CollectedItemInfo> CollectItemsForCategory(ItemWheelCategory category)
        {
            if (_inventory?.Content == null)
            {
                return new List<CollectedItemInfo>();
            }

            // 🆕 判断是否需要堆叠
            bool enableStacking = ItemCollector.ShouldStack(category);

            // 🆕 近战特殊处理：包括装备槽中的武器
            if (category == ItemWheelCategory.Melee)
            {
                return ItemCollector.CollectMelee(
                    _inventory,
                    _character,
                    item => MatchesCategoryStatic(item, category),
                    ModSettingFacade.Settings
                );
            }

            // 🆕 其他类别统一使用 ItemCollector
            return ItemCollector.Collect(
                _inventory,
                category,
                item => MatchesCategoryStatic(item, category),
                ModSettingFacade.Settings,
                enableStacking
            );
        }

        /// <summary>
        /// 检查物品是否匹配指定类别
        /// 通过物品标签映射来判断物品类别
        /// </summary>
        /// <param name="item">要检查的物品</param>
        /// <param name="category">目标类别</param>
        /// <returns>是否匹配类别</returns>
        internal static bool MatchesCategoryStatic(Item item, ItemWheelCategory category)
        {
            if (item?.Tags == null)
            {
                Debug.Log($"[ItemWheel] MatchesCategory: Item {item?.DisplayName ?? "null"} has no tags");
                return false;
            }

            foreach (var tag in item.Tags)
                {

                if (tag == null || string.IsNullOrEmpty(tag.name))
                {
                    continue;
                }

                if (TagMappings.TryGetValue(tag.name, out ItemWheelCategory mapped))
                {
                    
                    if (mapped == category)
                    {
                        Debug.Log($"[ItemWheel] MatchesCategory: Found match! Item {item.DisplayName} matches category {category}");
                        return true;
                    }
                }
                else
                {
                    // Tag not in mappings, continue checking next tag
                }
            }
            return false;
        }

        /// <summary>
        /// 🆕 阶段4：使用物品的核心方法（Handler模式）
        /// 根据物品类别委托给对应的Handler处理
        /// </summary>
        /// <param name="item">要使用的物品</param>
        /// <param name="category">物品类别</param>
        private void UseItem(Item item, ItemWheelCategory category)
        {
            CharacterMainControl character = CharacterMainControl.Main ?? _character;
            if (character == null || item == null)
            {
                return;
            }

            // 🆕 阶段4：使用Handler处理
            if (_handlers != null && _handlers.TryGetValue(category, out var handler))
            {
                CategoryWheel wheel = null;
                _wheels?.TryGetValue(category, out wheel);
                handler.UseItem(item, character, wheel);
            }
            else
            {
                // 降级方案：直接使用
                TryUseItemDirectly(item, character);
            }
        }

        private static void TryUseItemDirectly(Item item, CharacterMainControl character)
        {
            if (item?.UsageUtilities != null && item.UsageUtilities.IsUsable(item, character))
            {
                // 🆕 使用物品前，订阅销毁事件（用于容器物品消失后刷新快捷栏）
                SubscribeToItemDestroy(item);

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
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[ItemWheel] Show not-usable hint failed: {e.Message}");
                }
            }
        }

        private static void EquipItemToHand(Item item, CharacterMainControl character)
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
                TryUseItemDirectly(item, character);
            }
        }

        /// <summary>
        /// 🆕 阶段3：拖拽验证实现
        /// 检查槽位是否可拖拽，只允许主背包顶层的单物品拖拽
        /// </summary>
        private (bool canDrag, string reason) CanDragSlotImpl(CategoryWheel wheel, int slotIndex)
        {
            // 基础检查：槽位索引有效
            if (slotIndex < 0 || slotIndex >= wheel.DisplayedItems.Count)
            {
                return (false, "无效槽位");
            }

            // 获取物品信息
            var itemInfo = wheel.DisplayedItems[slotIndex];
            if (itemInfo.Item == null)
            {
                // 空槽位不能拖拽
                return (false, "空槽位");
            }

            // 检查1：堆叠物品不可拖拽
            if (itemInfo.StackCount > 1)
            {
                BubbleNotifier.Show("堆叠的，拖不动");
                return (false, "堆叠物品");
            }

            // 检查2：只有主背包顶层物品可拖拽
            if (!itemInfo.IsDraggable)
            {
                // 根据来源显示不同提示
                if (itemInfo.IsFromPet && itemInfo.IsFromSlot)
                {
                    BubbleNotifier.Show("狗子容器里的，别动");
                }
                else if (itemInfo.IsFromPet)
                {
                    BubbleNotifier.Show("狗子的东西，别动");
                }
                else if (itemInfo.IsFromSlot)
                {
                    BubbleNotifier.Show("在容器里，拖不了");
                }
                return (false, "非主背包物品");
            }

            // 允许拖拽
            return (true, null);
        }

        /// <summary>
        /// 🆕 处理轮盘槽位交换事件：当玩家拖拽轮盘物品时，同步到背包
        /// 参考 MainBackpackWheelManager.OnWheelSlotsSwapped (行1247-1261)
        /// 参考 MainBackpackWheelManager.AdjustWheelPosition (行1271-1374)
        /// </summary>
        private void OnWheelSlotsSwapped(CategoryWheel wheel, int fromWheelPos, int toWheelPos)
        {
            // 🚨 关键防护：如果已经在执行交换，直接返回，防止递归调用
            if (_isPerformingSwap)
            {
                Debug.Log($"[轮盘] ⚠️ 交换已在进行中，跳过重复调用");
                return;
            }

            if (fromWheelPos < 0 || fromWheelPos >= 8 || toWheelPos < 0 || toWheelPos >= 8)
            {
                return;
            }

            // 🆕 双重验证：检查两个槽位是否都可以拖拽
            // 虽然 CanDragSlot 已经阻止了拖拽开始，但强行 drop 仍可能触发交换事件
            var (canDragFrom, reasonFrom) = CanDragSlotImpl(wheel, fromWheelPos);
            var (canDragTo, reasonTo) = CanDragSlotImpl(wheel, toWheelPos);

            if (!canDragFrom || !canDragTo)
            {
                Debug.Log($"[轮盘] ✗ 阻止非法交换: from={fromWheelPos}({reasonFrom}), to={toWheelPos}({reasonTo})");
                // 刷新轮盘显示，恢复正确的顺序
                RefreshCategorySlots(wheel, resetSelection: false);
                return;
            }

            if (wheel.WheelToBackpackMapping == null || wheel.BackpackToWheelMapping == null)
            {
                return;
            }

            int fromBackpackPos = wheel.WheelToBackpackMapping[fromWheelPos];
            int toBackpackPos = wheel.WheelToBackpackMapping[toWheelPos];

            if (fromBackpackPos == -1 || toBackpackPos == -1)
            {
                return;
            }

            var item = _inventory.GetItemAt(fromBackpackPos);
            var targetItem = _inventory.GetItemAt(toBackpackPos);

            if (item == null || targetItem == null)
            {
                return;
            }

            Debug.Log($"[轮盘] {wheel.Category} 拖拽交换: 轮盘{fromWheelPos}↔{toWheelPos}, 背包{fromBackpackPos}({item.DisplayName})↔{toBackpackPos}({targetItem.DisplayName})");

            // 设置标志，防止递归：背包变化不应该再次触发轮盘更新
            _isPerformingSwap = true;

            try
            {
                // 直接交换 Content 数组中的索引（避免 Detach 导致 InInventory 为 null）
                var temp = _inventory.Content[fromBackpackPos];
                _inventory.Content[fromBackpackPos] = _inventory.Content[toBackpackPos];
                _inventory.Content[toBackpackPos] = temp;

                // 手动触发变更事件（重要！否则UI和其他监听器不会更新）
                TriggerInventoryContentChanged(_inventory, fromBackpackPos);
                TriggerInventoryContentChanged(_inventory, toBackpackPos);

                // 重新计算重量（保持一致性）
                _inventory.RecalculateWeight();

                // 更新映射关系（双向交换）
                wheel.WheelToBackpackMapping[fromWheelPos] = toBackpackPos;
                wheel.WheelToBackpackMapping[toWheelPos] = fromBackpackPos;
                wheel.BackpackToWheelMapping[toBackpackPos] = fromWheelPos;
                wheel.BackpackToWheelMapping[fromBackpackPos] = toWheelPos;

                // 🆕 选中状态跟随物品移动
                if (wheel.LastConfirmedIndex == fromWheelPos)
                {
                    wheel.LastConfirmedIndex = toWheelPos;
                    wheel.LastSelectedItem = item;  // 物品也跟着走
                    Debug.Log($"[轮盘] 选中跟随: {fromWheelPos} -> {toWheelPos}");
                }
                else if (wheel.LastConfirmedIndex == toWheelPos)
                {
                    wheel.LastConfirmedIndex = fromWheelPos;
                    wheel.LastSelectedItem = targetItem;  // 物品也跟着走
                    Debug.Log($"[轮盘] 选中跟随: {toWheelPos} -> {fromWheelPos}");
                }

                Debug.Log($"[轮盘] 背包交换完成");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[轮盘] ✗ 背包交换失败: {ex.Message}");
            }
            finally
            {
                _isPerformingSwap = false;
            }
        }

        /// <summary>
        /// 创建默认映射（按背包顺序）
        /// </summary>
        private void CreateDefaultMapping(CategoryWheel wheel, List<CollectedItemInfo> collected, Item[] slotBuffer)
        {
            // 清空旧映射
            System.Array.Fill(wheel.WheelToBackpackMapping, -1);
            wheel.BackpackToWheelMapping.Clear();
            System.Array.Fill(wheel.IsFromSlot, false);

            int bufferIndex = 0;
            foreach (CollectedItemInfo itemInfo in collected)
            {
                // 跳过索引8（中心位置）
                if (bufferIndex == 8)
                {
                    bufferIndex++;
                }

                if (bufferIndex >= slotBuffer.Length)
                {
                    break;
                }

                slotBuffer[bufferIndex] = itemInfo.Item;

                // 记录物品来源
                wheel.IsFromSlot[bufferIndex] = itemInfo.IsFromSlot;

                // 建立映射关系（只对背包物品建立映射，插槽物品不参与映射）
                if (!itemInfo.IsFromSlot)
                {
                    int backpackPos = itemInfo.BackpackIndex;
                    if (backpackPos >= 0)
                    {
                        wheel.WheelToBackpackMapping[bufferIndex] = backpackPos;
                        wheel.BackpackToWheelMapping[backpackPos] = bufferIndex;
                        Debug.Log($"[ItemWheel] Mapping: wheel[{bufferIndex}] <-> backpack[{backpackPos}] ({itemInfo.Item.DisplayName})");
                    }
                }
                else
                {
                    Debug.Log($"[ItemWheel] Slot item: wheel[{bufferIndex}] = {itemInfo.Item.DisplayName} (from slot, not draggable)");
                }

                bufferIndex++;
            }
        }


        /// <summary>
        /// 🆕 阶段3：订阅物品销毁事件
        /// 用于处理容器物品使用后快捷栏自动更新
        /// </summary>
        internal static void SubscribeToItemDestroy(Item item)
        {
            if (item == null || item.gameObject == null)
            {
                return;
            }

            // 查找物品属于哪个类别
            ItemWheelCategory? category = null;
            if (item.Tags != null)
            {
                foreach (var tag in item.Tags)
                {
                    if (tag != null && TagMappings.TryGetValue(tag.name, out ItemWheelCategory cat))
                    {
                        category = cat;
                        break;
                    }
                }
            }

            if (!category.HasValue)
            {
                // 不属于ItemWheel管理的类别（如子弹），跳过
                return;
            }

            // 添加销毁监听组件
            var listener = item.gameObject.AddComponent<ItemDestroyListener>();
            listener.Category = category.Value;
            Debug.Log($"[ItemWheel] 🔔 订阅物品销毁事件: {item.DisplayName} -> {category.Value}");
        }

        /// <summary>
        /// 🆕 阶段3：刷新指定类别的快捷栏（物品销毁后调用）
        /// </summary>
        private static void RefreshShortcutAfterItemDestroy(ItemWheelCategory category)
        {
            if (_instance == null || !_instance._wheels.TryGetValue(category, out CategoryWheel wheel))
            {
                return;
            }

            Debug.Log($"[ItemWheel] 🔄 物品销毁，刷新快捷栏: {category}");

            // 刷新该类别的槽位（不修改快捷栏，下面会手动同步）
            _instance.RefreshCategorySlots(wheel, resetSelection: false, skipShortcutSync: true);

            // 选择下一个可用物品
            int newIndex = GetFirstAvailableItemIndex(wheel);
            wheel.LastConfirmedIndex = newIndex;
            if (newIndex >= 0)
            {
                wheel.LastSelectedItem = wheel.Slots[newIndex];
            }

            // 同步到快捷栏（非近战类别）
            if (category != ItemWheelCategory.Melee && newIndex >= 0)
            {
                var shortcutIndex = (int)category;
                var newItem = wheel.Slots[newIndex];
                Duckov.ItemShortcut.Set(shortcutIndex, newItem);
                Debug.Log($"[ItemWheel] ✅ 快捷栏已更新: 槽位{shortcutIndex} -> {newItem?.DisplayName ?? "null"}");
            }
        }

        /// <summary>
        /// 🆕 阶段3：物品销毁监听器（MonoBehaviour组件）
        /// 当物品GameObject被销毁时，自动刷新对应类别的快捷栏
        /// </summary>
        private class ItemDestroyListener : MonoBehaviour
        {
            public ItemWheelCategory Category;

            private void OnDestroy()
            {
                // GameObject销毁时触发快捷栏刷新
                RefreshShortcutAfterItemDestroy(Category);
            }
        }
    }
}
