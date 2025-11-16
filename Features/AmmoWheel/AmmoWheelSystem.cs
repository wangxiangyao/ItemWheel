using System;
using System.Collections.Generic;
using System.Linq;
using Duckov;
using ItemStatsSystem;
using QuickWheel.Core;
using QuickWheel.Selection;
using QuickWheel.UI;
using UnityEngine;
using QuickWheel.Utils;
using ItemWheel.UI;
using ItemWheel.Integration;
using ItemWheel.Core.ItemSources;

namespace ItemWheel
{
    /// <summary>
    /// 子弹轮盘系统（长按 R 呼出，短按 R 走原生换弹）。
    /// - 槽位：当前武器可用的所有弹种（按 TypeID 去重），代表物品取背包中第一枚该类型子弹
    /// - 交互：hover/点击均切换目标弹种并触发一次换弹，然后关闭轮盘
    /// </summary>
    public sealed class AmmoWheelSystem
    {

        private class KeyState
        {
            public bool IsPressed;
            public float HoldTime;
            public bool HasTriggeredWheel;
            public Vector2 PressedMousePosition;
        }

        private readonly KeyState _state = new KeyState();

        private Wheel<Item> _wheel;
        private DefaultWheelView<Item> _view;
        private QuickWheel.Input.MouseWheelInput _input;
        private Item[] _slots = Array.Empty<Item>();

        private readonly Dictionary<int, Item> _typeToItem = new Dictionary<int, Item>();
        private Dictionary<int, int> _bulletTypeCounts = new Dictionary<int, int>();

        // 关闭与回调防抖
        private bool _isClosing;
        private bool _skipOnHidden;

        public bool HasActiveWheel => _wheel != null && _wheel.IsVisible;

        public void Update()
        {
            HandleLongPressTimer();
            if (HasActiveWheel)
            {
                _wheel.Update();
            }
        }

        public void OnKeyPressed()
        {
            Debug.Log("[AmmoWheel] R键按下");
            _state.IsPressed = true;
            _state.HoldTime = 0f;
            _state.HasTriggeredWheel = false;
            _state.PressedMousePosition = UnityEngine.Input.mousePosition;
            EnsureWheel();
        }

        public void OnKeyReleased()
        {
            _state.IsPressed = false;

            if (_state.HasTriggeredWheel)
            {
                // 长按：确认当前 hover 的选择
                ConfirmSelectionAndHide();
            }
            else
            {
                // 短按：原生换弹
                TryNativeReload(null);
            }

            _state.HoldTime = 0f;
            _state.HasTriggeredWheel = false;
        }

        private void HandleLongPressTimer()
        {
            if (_state.IsPressed && !_state.HasTriggeredWheel)
            {
                _state.HoldTime += Time.unscaledDeltaTime;
                const float threshold = 0.2f;
                if (_state.HoldTime >= threshold)
                {
                    Debug.Log($"[AmmoWheel] 检测到长按 (HoldTime={_state.HoldTime:F2}s)");
                    _state.HasTriggeredWheel = true;
                    ShowWheel(_state.PressedMousePosition);
                }
            }
        }

        private void EnsureWheel()
        {
            if (_wheel != null)
            {
                return;
            }

            // 🆕 使用统一的 WheelSpriteLoader
            WheelSpriteLoader.Load();

            _input = new QuickWheel.Input.MouseWheelInput();
            _view = new DefaultWheelView<Item>();

            _wheel = new WheelBuilder<Item>()
                .WithConfig(cfg =>
                {
                    cfg.EnablePersistence = false;
                    cfg.GridCellSize = 90f;
                    cfg.GridSpacing = 12f;
                    cfg.DeadZoneRadius = 40f; // 死区半径（像素）

                    // 🆕 启用点击选择（点击和hover松开都能换弹）
                    cfg.EnableClickSelect = true;

                    // 🆕 使用 WheelSpriteLoader 加载的自定义格子Sprite
                    cfg.SlotNormalSprite = WheelSpriteLoader.SlotNormal;
                    cfg.SlotHoverSprite = WheelSpriteLoader.SlotHover;
                    cfg.SlotSelectedSprite = WheelSpriteLoader.SlotSelected;

                    // 子弹拖拽验证：子弹是堆叠物品，全部禁止拖拽
                    cfg.CanDragSlot = (slotIndex) =>
                    {
                        BubbleNotifier.Show("子弹拖不了");
                        return (false, "堆叠物品");
                    };
                })
                .WithAdapter(new BulletWheelAdapter(_bulletTypeCounts))

                .WithView(_view)

                .WithInput(_input)

                .WithSelectionStrategy(new GridSelectionStrategy())

                .OnItemSelected((index, item) => OnItemSelected(index, item))

                .OnWheelShown(WheelInputGuard.OnWheelShown)

                .OnWheelHidden(index =>

                {

                    WheelInputGuard.OnWheelHidden();

                    OnWheelHidden(index);

                })

                .Build();

            _slots = new Item[WheelConfig.SLOT_COUNT];
            _wheel.SetSlots(_slots);
        }

        private void ShowWheel(Vector2 center)
        {
            // 🆕 检查 ModSetting 配置
            if (!ModSettingFacade.Settings.EnableAmmoWheel)
            {
                Debug.Log("[AmmoWheel] 子弹轮盘已在配置中禁用");
                return;
            }

            _isClosing = false;
            _skipOnHidden = false;

            Debug.Log("[AmmoWheel] 开始刷新子弹槽位...");
            if (!RefreshSlots())
            {
                Debug.Log("[AmmoWheel] 没有可用子弹或未装备枪械，不显示轮盘");
                return;
            }

            Debug.Log($"[AmmoWheel] 子弹槽位刷新完成，显示轮盘");
            _view?.SetWheelCenterBeforeShow(center);
            _input?.SetPressedState(true);
            _wheel?.Show();

            int preferredIndex = GetPreferredIndex();
            if (preferredIndex >= 0)
            {
                _wheel.SetSelectedIndex(preferredIndex);
            }

            // 🆕 启用子弹时间
            ItemWheelSystem.EnableBulletTime();
        }

        private void ConfirmSelectionAndHide()
        {
            if (_wheel == null)
            {
                return;
            }
            _wheel.ManualConfirm();
        }

        private void OnItemSelected(int index, Item item)
        {
            if (_isClosing)
            {
                return;
            }
            _isClosing = true;
            _skipOnHidden = true; // 点击已处理切换，隐藏回调不再重复
            if (item != null)
            {
                SwitchAmmo(item);
            }
            _wheel.Hide();

            // 🆕 禁用子弹时间
            ItemWheelSystem.DisableBulletTime();
        }

        private void OnWheelHidden(int index)
        {
            Debug.Log($"[AmmoWheel] 🔵 OnWheelHidden called: index={index}, _skipOnHidden={_skipOnHidden}, _isClosing={_isClosing}");

            if (_skipOnHidden)
            {
                Debug.Log($"[AmmoWheel] ⏭️ OnWheelHidden skipped (_skipOnHidden=true)");
                _skipOnHidden = false;
                _isClosing = false;
                return;
            }

            if (_slots == null || index < 0 || index >= _slots.Length)
            {
                Debug.LogWarning($"[AmmoWheel] ❌ OnWheelHidden: Invalid index or slots. _slots={_slots != null}, index={index}, length={_slots?.Length}");
                // 🆕 禁用子弹时间
                ItemWheelSystem.DisableBulletTime();
                return;
            }
            var item = _slots[index];
            Debug.Log($"[AmmoWheel] 🔵 OnWheelHidden: item at index {index} = {item?.DisplayName ?? "null"}");

            if (item != null)
            {
                Debug.Log($"[AmmoWheel] 🔫 Switching ammo to: {item.DisplayName}");
                SwitchAmmo(item);
            }
            else
            {
                Debug.LogWarning($"[AmmoWheel] ❌ Item is null at index {index}");
            }

            _isClosing = false;

            // 🆕 禁用子弹时间
            Debug.Log($"[AmmoWheel] ⏱️ Disabling bullet time");
            ItemWheelSystem.DisableBulletTime();
        }

        private bool RefreshSlots()
        {
            _typeToItem.Clear();

            var character = CharacterMainControl.Main;
            var gun = character?.GetGun();
            var inventory = character?.CharacterItem?.Inventory;
            if (gun == null || gun.GunItemSetting == null || inventory == null)
            {
                return false;
            }

            var inventories = InventorySearcher.GetInventoriesToSearch(
                inventory,
                ModSettingFacade.Settings.SearchInPetInventory);
            if (inventories.Count == 0)
            {
                return false;
            }

            var combinedTypes = CollectBulletCounts(gun, inventories);
            _bulletTypeCounts.Clear();
            foreach (var kv in combinedTypes)
            {
                _bulletTypeCounts[kv.Key] = kv.Value;
            }

            if (combinedTypes.Count == 0)
            {
                _slots = new Item[WheelConfig.SLOT_COUNT];
                _wheel.SetSlots(_slots);
                return false;
            }

            var list = new List<Item>();
            foreach (var kv in combinedTypes)
            {
                int typeId = kv.Key;
                var rep = FindFirstItemOfType(inventories, typeId);
                if (rep != null)
                {
                    _typeToItem[typeId] = rep;
                    list.Add(rep);
                }
            }

            var buffer = new Item[WheelConfig.SLOT_COUNT];
            int idx = 0;
            foreach (var it in list.Take(WheelConfig.SLOT_COUNT - 1))
            {
                if (idx == 8) idx++;
                buffer[idx++] = it;
            }

            _slots = buffer;
            _wheel.SetSlots(_slots);
            return true;
        }

        private static Item FindFirstItemOfType(IEnumerable<Inventory> inventories, int typeId)
        {
            var options = new InventorySearchOptions(
                inventories,
                item => item != null && item.TypeID == typeId,
                ModSettingFacade.Settings,
                CharacterMainControl.Main);
            return InventorySearcher.FindFirst(options)?.Item;
        }

        private static Dictionary<int, int> CollectBulletCounts(ItemAgent_Gun gun, IEnumerable<Inventory> inventories)
        {
            var combined = new Dictionary<int, int>();
            foreach (var inv in inventories)
            {
                if (inv == null)
                {
                    continue;
                }
                try
                {
                    var types = gun.GunItemSetting.GetBulletTypesInInventory(inv);
                    if (types == null)
                    {
                        continue;
                    }
                    foreach (var kv in types)
                    {
                        int count = kv.Value?.count ?? 0;
                        if (count <= 0)
                        {
                            continue;
                        }
                        if (combined.TryGetValue(kv.Key, out var existing))
                        {
                            combined[kv.Key] = existing + count;
                        }
                        else
                        {
                            combined[kv.Key] = count;
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }
            return combined;
        }

        private int GetPreferredIndex()
        {
            var character = CharacterMainControl.Main;
            var gun = character?.GetGun();
            if (gun == null)
            {
                return -1;
            }

            // 1) 当前已装弹
            Item loaded = gun.GunItemSetting.GetCurrentLoadedBullet();
            if (loaded != null)
            {
                int idx = Array.IndexOf(_slots, loaded);
                if (idx >= 0) return idx;
            }

            // 2) 目标弹种（TargetBulletID）
            int targetId = gun.GunItemSetting.TargetBulletID;
            if (targetId >= 0)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    var it = _slots[i];
                    if (it != null && it.TypeID == targetId) return i;
                }
            }

            // 3) 退化：第一个非空
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i == 8) continue;
                if (_slots[i] != null) return i;
            }
            return -1;
        }

        private void SwitchAmmo(Item bulletItem)
        {
            var character = CharacterMainControl.Main;
            var gun = character?.GetGun();
            if (character == null || gun == null || bulletItem == null)
            {
                return;
            }

            try
            {
                gun.GunItemSetting.SetTargetBulletType(bulletItem.TypeID);
                character.TryToReload(bulletItem);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AmmoWheel] 切换弹种失败: {ex.Message}");
            }
        }

        private static void TryNativeReload(Item prefered)
        {
            var ch = CharacterMainControl.Main;
            try { ch?.TryToReload(prefered); } catch { }
        }

        // 🗑️ LoadCustomSprites 方法已移除，使用统一的 ItemWheel.UI.SpriteLoader 替代
    }
}
