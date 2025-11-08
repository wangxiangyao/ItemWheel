using System;
using System.Collections.Generic;
using System.Linq;
using ItemStatsSystem;
using QuickWheel.Core;
using QuickWheel.UI;
using QuickWheel.Utils;
using QuickWheel.Selection;
using UnityEngine;

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

        private sealed class CategoryWheel
        {
            public ItemWheelCategory Category;
            public Wheel<Item> Wheel;
            public Item[] Slots;
            public int LastConfirmedIndex;
            public QuickWheel.Input.MouseWheelInput Input;  // ✅ 保存输入处理器引用
            public DefaultWheelView<Item> View;  // ⭐ 保存View引用用于设置中心位置

            // 🆕 双向映射机制：轮盘位置 ↔ 背包位置
            public int[] WheelToBackpackMapping;              // 轮盘位置[0-7] → 背包位置
            public Dictionary<int, int> BackpackToWheelMapping; // 背包位置 → 轮盘位置

            public CategoryWheel()
            {
                // 初始化映射数据结构（8个轮盘位置）
                WheelToBackpackMapping = new int[8];
                System.Array.Fill(WheelToBackpackMapping, -1); // -1 表示空位
                BackpackToWheelMapping = new Dictionary<int, int>();
            }
        }

        [System.NonSerialized]
        private Dictionary<ItemWheelCategory, CategoryWheel> _wheels;

        [System.NonSerialized]
        private CharacterMainControl _character;

        [System.NonSerialized]
        private Inventory _inventory;

        // 自定义格子Sprite
        private static Sprite _slotNormalSprite;
        private static Sprite _slotHoverSprite;
        private static Sprite _slotSelectedSprite;

        // 🆕 防止递归事件标志：轮盘拖拽时同步背包，避免触发背包变化事件再次更新轮盘
        private bool _isPerformingSwap = false;

        // 🆕 映射持久化系统
        private static WheelMappingPersistence _mappingPersistence;

        public ItemWheelSystem()
        {
            _wheels = new Dictionary<ItemWheelCategory, CategoryWheel>();
            LevelManager.OnLevelInitialized += HandleLevelInitialized;

            // 加载自定义格子Sprite
            LoadCustomSprites();

            // 初始化持久化系统
            InitializePersistence();
        }

        /// <summary>
        /// 从Mod目录加载自定义格子Sprite
        /// </summary>
        private static void LoadCustomSprites()
        {
            if (_slotNormalSprite != null) return;  // 已经加载过了

            try
            {
                // 获取Mod目录路径
                string modPath = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location
                );
                string texturePath = System.IO.Path.Combine(modPath, "texture");

                // 加载三个状态的Sprite
                string normalPath = System.IO.Path.Combine(texturePath, "WheelSlot_Normal.png");
                string hoverPath = System.IO.Path.Combine(texturePath, "WheelSlot_Hover.png");
                string selectedPath = System.IO.Path.Combine(texturePath, "WheelSlot_Selected.png");

                _slotNormalSprite = SpriteLoader.LoadFromFile(normalPath, 100f);
                _slotHoverSprite = SpriteLoader.LoadFromFile(hoverPath, 100f);
                _slotSelectedSprite = SpriteLoader.LoadFromFile(selectedPath, 100f);

                if (_slotNormalSprite != null)
                {
                    Debug.Log("[ItemWheel] Custom slot sprites loaded successfully");
                }
                else
                {
                    Debug.LogWarning("[ItemWheel] Failed to load custom slot sprites, will use default colors");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ItemWheel] Error loading custom sprites: {e}");
            }
        }

        /// <summary>
        /// 初始化映射持久化系统
        /// </summary>
        private static void InitializePersistence()
        {
            if (_mappingPersistence != null) return;  // 已经初始化过了

            try
            {
                // 获取Mod目录路径
                string modPath = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location
                );

                _mappingPersistence = new WheelMappingPersistence(modPath);
                Debug.Log("[ItemWheel] Mapping persistence initialized");

                // 检查是否有保存的映射
                if (_mappingPersistence.HasSavedMappings())
                {
                    Debug.Log("[ItemWheel] Found saved wheel mappings");
                }
                else
                {
                    Debug.Log("[ItemWheel] No saved wheel mappings found (first time use)");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ItemWheel] Failed to initialize persistence: {ex.Message}");
            }
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
            var wheel = EnsureWheel(category);
            bool isFirstLoad = wheel.Slots == null || wheel.Slots.All(s => s == null);

            // 打开轮盘时不重置选择，保持之前选中的物品
            if (!RefreshCategorySlots(wheel, reloadFromFile: isFirstLoad, resetSelection: false))
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

            // 新一轮显示，重置“本次是否交换”标记
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

        /// <summary>
        /// 按键按下事件（由ModBehavior调用）
        /// 开始长按计时
        /// </summary>
        /// <param name="category">物品类别</param>
        public void OnKeyPressed(ItemWheelCategory category)
        {
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
                UseShortcutDirect(category);
            }

            // 重置状态
            state.HoldTime = 0f;
            state.HasTriggeredWheel = false;
        }

        /// <summary>
        /// 直接使用快捷物品（避免循环调用）
        /// </summary>
        /// <param name="category">物品类别</param>
        private void UseShortcutDirect(ItemWheelCategory category)
        {
            if (!_wheels.TryGetValue(category, out var wheel))
            {
                return;  // 轮盘还未创建，忽略
            }

            // 短按不应触发重排/重建布局，避免快捷键UI变化
            // 仅在首次未初始化时刷新一次（并且不重置选择）
            if (wheel.Slots == null || wheel.Slots.All(s => s == null))
            {
                if (!RefreshCategorySlots(wheel, reloadFromFile: false, resetSelection: false))
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
            UseItem(item, category);
        }

        /// <summary>
        /// 每帧更新方法（处理长按计时和轮盘逻辑）
        /// </summary>
        public void Update()
        {
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
            if (_wheels.TryGetValue(category, out var wheel))
            {
                // 若本次显示期间发生过交换，关闭时不使用物品，直接取消
                if (_sessionSwapped.TryGetValue(category, out bool swapped) && swapped)
                {
                    Debug.Log($"[轮盘] 本次发生过交换，关闭时取消选择: {category}");
                    wheel.Wheel?.ManualCancel();
                }
                else
                {
                    wheel.Wheel?.ManualConfirm();
                }
            }
        }

        /// <summary>
        /// 释放资源方法
        /// 清理所有轮盘实例和字典数据
        /// </summary>
        public void Dispose()
        {
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

            _wheels.Clear();
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
            }
        }

        /// <summary>
        /// 🆕 背包内容变化事件处理器
        /// 当背包中物品位置变化时，刷新轮盘映射
        /// </summary>
        private void OnInventoryContentChanged(Inventory inventory, int changedSlot)
        {
            // 🆕 在交换过程中跳过处理，避免递归
            if (_isPerformingSwap)
            {
                Debug.Log($"[轮盘] ⚠️ 背包变化(slot={changedSlot})被跳过，正在执行交换");
                return;
            }

            Debug.Log($"[轮盘] 背包变化: slot={changedSlot}，刷新所有类别并重置选择");

            foreach (var kvp in _wheels)
            {
                // 背包变化时重置选择为第一个
                RefreshCategorySlots(kvp.Value, reloadFromFile: false, resetSelection: true);
            }
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

            Wheel<Item> wheel = new WheelBuilder<Item>()
                .WithConfig(cfg =>
                {
                    cfg.EnablePersistence = false;
                    cfg.GridCellSize = 90f;  // 格子大小（像素）
                    cfg.GridSpacing = 12f;   // 格子间距（像素）

                    // 使用自定义格子Sprite
                    cfg.SlotNormalSprite = _slotNormalSprite;
                    cfg.SlotHoverSprite = _slotHoverSprite;
                    cfg.SlotSelectedSprite = _slotSelectedSprite;
                })
                .WithAdapter(new ItemWheelAdapter())
                .WithView(view)  // ⭐ 使用创建的View实例
                .WithInput(input)  // ✨ 只处理鼠标移动，不处理按键
                .WithSelectionStrategy(new GridSelectionStrategy())
                .OnItemSelected((index, item) => OnItemSelected(context, index, item))
                .OnWheelHidden(index => OnWheelHidden(context, index))
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

        private bool RefreshCategorySlots(CategoryWheel wheel, bool reloadFromFile = false, bool resetSelection = true)
        {
            if (_isPerformingSwap)
            {
                return true;
            }

            if (_inventory == null)
            {
                return false;
            }

            List<Item> collected = CollectItemsForCategory(wheel.Category);

            if (collected.Count == 0)
            {
                wheel.Slots = new Item[WheelConfig.SLOT_COUNT];
                wheel.Wheel.SetSlots(wheel.Slots);
                return false;
            }

            Item[] slotBuffer = new Item[WheelConfig.SLOT_COUNT];

            bool usingSavedMapping = false;

            if (reloadFromFile)
            {
                usingSavedMapping = TryLoadSavedMapping(wheel, collected, slotBuffer);
            }

            if (!usingSavedMapping)
            {
                CreateDefaultMapping(wheel, collected, slotBuffer);
            }

            wheel.Slots = slotBuffer;
            wheel.Wheel.SetSlots(slotBuffer);

            // 根据 resetSelection 参数决定是否重置选择
            if (resetSelection)
            {
                // 背包变化时：总是选择第一个
                wheel.LastConfirmedIndex = GetFirstAvailableIndex(slotBuffer);
            }
            else
            {
                // 只是打开轮盘时：如果之前的选择还存在就保持，否则选第一个
                if (wheel.LastConfirmedIndex < 0 || wheel.LastConfirmedIndex >= slotBuffer.Length || slotBuffer[wheel.LastConfirmedIndex] == null)
                {
                    wheel.LastConfirmedIndex = GetFirstAvailableIndex(slotBuffer);
                }
            }

            // 更新快捷栏UI（近战不更新官方快捷栏，避免错位）
            if (wheel.LastConfirmedIndex >= 0 && wheel.Category != ItemWheelCategory.Melee)
            {
                var shortcutIndex = (int)wheel.Category;
                Duckov.ItemShortcut.Set(shortcutIndex, slotBuffer[wheel.LastConfirmedIndex]);
            }

            return true;
        }

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
                return wheel.LastConfirmedIndex;
            }

            return GetFirstAvailableIndex(wheel.Slots);
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
                UseItem(item, wheel.Category);
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

            wheel.LastConfirmedIndex = selectedIndex;

            if (selectedIndex >= 0 && selectedIndex < wheel.Slots.Length && wheel.Slots[selectedIndex] != null)
            {
                // 同步官方快捷栏（近战不更新官方快捷栏）
                if (wheel.Category != ItemWheelCategory.Melee)
                {
                    var shortcutIndex = (int)wheel.Category;
                    Duckov.ItemShortcut.Set(shortcutIndex, wheel.Slots[selectedIndex]);
                }
                Debug.Log($"[轮盘] {wheel.Category} 点击选中: 位置{selectedIndex} {wheel.Slots[selectedIndex].DisplayName}");
            }
        }

        /// <summary>
        /// 从物品栏收集指定类别的所有物品
        /// 按照物品栏顺序收集，最多收集8个物品（中心空位）
        /// </summary>
        /// <param name="category">要收集的物品类别</param>
        /// <returns>物品列表</returns>
        private List<Item> CollectItemsForCategory(ItemWheelCategory category)
        {
            var result = new List<Item>(WheelConfig.SLOT_COUNT - 1);

            if (_inventory?.Content == null)
            {
                return result;
            }

            foreach (Item item in _inventory.Content)
            {
                if (item == null || !MatchesCategory(item, category))
                {
                    continue;
                }

                result.Add(item);

                if (result.Count >= WheelConfig.SLOT_COUNT - 1)
                {
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// 检查物品是否匹配指定类别
        /// 通过物品标签映射来判断物品类别
        /// </summary>
        /// <param name="item">要检查的物品</param>
        /// <param name="category">目标类别</param>
        /// <returns>是否匹配类别</returns>
        private static bool MatchesCategory(Item item, ItemWheelCategory category)
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
        /// 使用物品的核心方法
        /// 根据物品类别选择合适的使用方式（直接使用或装备）
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

            switch (category)
            {
                case ItemWheelCategory.Medical:
                case ItemWheelCategory.Stim:
                case ItemWheelCategory.Food:
                    TryUseItemDirectly(item, character);
                    break;
                case ItemWheelCategory.Explosive:
                case ItemWheelCategory.Melee:
                    EquipItemToHand(item, character);
                    break;
                default:
                    TryUseItemDirectly(item, character);
                    break;
            }
        }

        private static void TryUseItemDirectly(Item item, CharacterMainControl character)
        {
            if (item?.UsageUtilities != null && item.UsageUtilities.IsUsable(item, character))
            {
                character.UseItem(item);
                // 使用成功（满足 IsUsable）后，重置“不可使用”情绪计数回到平静
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
                // 从背包中取出两个物品
                item.Detach();
                targetItem.Detach();

                // 交换位置重新放入
                _inventory.AddAt(targetItem, fromBackpackPos);
                _inventory.AddAt(item, toBackpackPos);

                // 更新映射关系（双向交换）
                wheel.WheelToBackpackMapping[fromWheelPos] = toBackpackPos;
                wheel.WheelToBackpackMapping[toWheelPos] = fromBackpackPos;
                wheel.BackpackToWheelMapping[toBackpackPos] = fromWheelPos;
                wheel.BackpackToWheelMapping[fromBackpackPos] = toWheelPos;

                // 🆕 选中状态跟随物品移动
                if (wheel.LastConfirmedIndex == fromWheelPos)
                {
                    wheel.LastConfirmedIndex = toWheelPos;
                    Debug.Log($"[轮盘] 选中跟随: {fromWheelPos} -> {toWheelPos}");
                }
                else if (wheel.LastConfirmedIndex == toWheelPos)
                {
                    wheel.LastConfirmedIndex = fromWheelPos;
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

            SaveAllMappings();
        }

        /// <summary>
        /// 尝试加载保存的映射并应用
        /// </summary>
        private bool TryLoadSavedMapping(CategoryWheel wheel, List<Item> collected, Item[] slotBuffer)
        {
            if (_mappingPersistence == null || !_mappingPersistence.HasSavedMappings())
            {
                return false;
            }

            try
            {
                var savedMappings = _mappingPersistence.Load();
                if (savedMappings == null || !savedMappings.ContainsKey(wheel.Category))
                {
                    return false;
                }

                var savedMapping = savedMappings[wheel.Category];

                // 清空旧映射
                System.Array.Fill(wheel.WheelToBackpackMapping, -1);
                wheel.BackpackToWheelMapping.Clear();

                // 🆕 检查是否至少有一个有效映射，如果全为-1则重新生成
                bool hasAnyValidMapping = false;
                for (int wheelPos = 0; wheelPos < 8; wheelPos++)
                {
                    if (savedMapping[wheelPos] >= 0)
                    {
                        hasAnyValidMapping = true;
                        break;
                    }
                }

                if (!hasAnyValidMapping)
                {
                    Debug.Log($"[ItemWheel] 🔄 No valid mappings found for {wheel.Category} (all -1), regenerating");
                    return false;
                }

                // 验证保存的映射 - 只要有一个映射失败就重新生成
                for (int wheelPos = 0; wheelPos < 8; wheelPos++)
                {
                    int backpackPos = savedMapping[wheelPos];
                    if (backpackPos < 0) continue;  // 空位跳过

                    // 验证：背包位置是否有效，且物品属于当前类别
                    if (backpackPos >= _inventory.Content.Count)
                    {
                        Debug.LogWarning($"[ItemWheel] 🚨 Mapping validation failed: backpack[{backpackPos}] out of range");
                        Debug.LogWarning($"[ItemWheel] 🔄 Regenerating mapping for {wheel.Category}");
                        return false;  // 🚫 一个失败就全部重新生成
                    }

                    var item = _inventory.GetItemAt(backpackPos);
                    if (item == null)
                    {
                        Debug.LogWarning($"[ItemWheel] 🚨 Mapping validation failed: backpack[{backpackPos}] is empty");
                        Debug.LogWarning($"[ItemWheel] 🔄 Regenerating mapping for {wheel.Category}");
                        return false;  // 🚫 一个失败就全部重新生成
                    }

                    // 检查物品是否在collected列表中（属于当前类别）
                    if (!collected.Contains(item))
                    {
                        Debug.LogWarning($"[ItemWheel] 🚨 Mapping validation failed: backpack[{backpackPos}] item '{item.DisplayName}' not in category {wheel.Category}");
                        Debug.LogWarning($"[ItemWheel] 🔄 Regenerating mapping for {wheel.Category}");
                        return false;  // 🚫 一个失败就全部重新生成
                    }
                }

                // 所有映射都验证通过，现在应用它们
                int validMappings = 0;
                for (int wheelPos = 0; wheelPos < 8; wheelPos++)
                {
                    int backpackPos = savedMapping[wheelPos];
                    if (backpackPos < 0) continue;  // 空位

                    var item = _inventory.GetItemAt(backpackPos);
                    // 映射有效，应用
                    slotBuffer[wheelPos] = item;
                    wheel.WheelToBackpackMapping[wheelPos] = backpackPos;
                    wheel.BackpackToWheelMapping[backpackPos] = wheelPos;
                    validMappings++;

                    Debug.Log($"[ItemWheel] ✓ Restored mapping: wheel[{wheelPos}] <-> backpack[{backpackPos}] ({item.DisplayName})");
                }

                Debug.Log($"[ItemWheel] ✅ All saved mappings validated for {wheel.Category}: {validMappings} mappings loaded");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ItemWheel] Failed to load saved mapping: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 创建默认映射（按背包顺序）
        /// </summary>
        private void CreateDefaultMapping(CategoryWheel wheel, List<Item> collected, Item[] slotBuffer)
        {
            // 清空旧映射
            System.Array.Fill(wheel.WheelToBackpackMapping, -1);
            wheel.BackpackToWheelMapping.Clear();

            int bufferIndex = 0;
            foreach (Item collectedItem in collected)
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

                slotBuffer[bufferIndex] = collectedItem;

                // 建立映射关系
                int backpackPos = _inventory.Content.IndexOf(collectedItem);
                if (backpackPos >= 0)
                {
                    wheel.WheelToBackpackMapping[bufferIndex] = backpackPos;
                    wheel.BackpackToWheelMapping[backpackPos] = bufferIndex;
                    Debug.Log($"[ItemWheel] Mapping: wheel[{bufferIndex}] <-> backpack[{backpackPos}] ({collectedItem.DisplayName})");
                }
                else
                {
                    Debug.LogWarning($"[ItemWheel] Item {collectedItem.DisplayName} not found in inventory");
                }

                bufferIndex++;
            }
        }

        /// <summary>
        /// 保存所有类别的映射
        /// </summary>
        private void SaveAllMappings()
        {
            if (_mappingPersistence == null)
            {
                Debug.LogWarning("[ItemWheel] Cannot save mappings: persistence system not initialized");
                return;
            }

            try
            {
                var allMappings = new Dictionary<ItemWheelCategory, int[]>();

                // 收集所有类别的映射
                foreach (var kvp in _wheels)
                {
                    var category = kvp.Key;
                    var wheel = kvp.Value;

                    // 复制映射数组
                    var mappingCopy = new int[8];
                    Array.Copy(wheel.WheelToBackpackMapping, mappingCopy, 8);
                    allMappings[category] = mappingCopy;
                }

                // 保存到文件
                _mappingPersistence.Save(allMappings);
                Debug.Log($"[ItemWheel] ✓ Saved mappings for {allMappings.Count} categories");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ItemWheel] Failed to save mappings: {ex.Message}");
            }
        }
    }
}
