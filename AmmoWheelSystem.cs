using System;
using System.Collections.Generic;
using System.Linq;
using ItemStatsSystem;
using QuickWheel.Core;
using QuickWheel.Selection;
using QuickWheel.UI;
using UnityEngine;
using QuickWheel.Utils;

namespace ItemWheel
{
    /// <summary>
    /// 子弹轮盘系统（长按 R 呼出，短按 R 走原生换弹）。
    /// - 槽位：当前武器可用的所有弹种（按 TypeID 去重），代表物品取背包中第一枚该类型子弹
    /// - 交互：hover/点击均切换目标弹种并触发一次换弹，然后关闭轮盘
    /// </summary>
    public sealed class AmmoWheelSystem
    {
        // 自定义格子Sprite（与物品轮盘一致）
        private static Sprite _slotNormalSprite;
        private static Sprite _slotHoverSprite;
        private static Sprite _slotSelectedSprite;

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

            LoadCustomSprites();

            _input = new QuickWheel.Input.MouseWheelInput();
            _view = new DefaultWheelView<Item>();

            _wheel = new WheelBuilder<Item>()
                .WithConfig(cfg =>
                {
                    cfg.EnablePersistence = false;
                    cfg.GridCellSize = 90f;
                    cfg.GridSpacing = 12f;
                    cfg.SlotNormalSprite = _slotNormalSprite;
                    cfg.SlotHoverSprite = _slotHoverSprite;
                    cfg.SlotSelectedSprite = _slotSelectedSprite;
                })
                .WithAdapter(new ItemWheelAdapter())
                .WithView(_view)
                .WithInput(_input)
                .WithSelectionStrategy(new GridSelectionStrategy())
                .OnItemSelected((index, item) => OnItemSelected(index, item))
                .OnWheelHidden(index => OnWheelHidden(index))
                .Build();

            _slots = new Item[WheelConfig.SLOT_COUNT];
            _wheel.SetSlots(_slots);
        }

        private void ShowWheel(Vector2 center)
        {
            _isClosing = false;
            _skipOnHidden = false;
            if (!RefreshSlots())
            {
                return;
            }

            _view?.SetWheelCenterBeforeShow(center);
            _input?.SetPressedState(true);
            _wheel?.Show();

            int preferredIndex = GetPreferredIndex();
            if (preferredIndex >= 0)
            {
                _wheel.SetSelectedIndex(preferredIndex);
            }
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
        }

        private void OnWheelHidden(int index)
        {
            if (_skipOnHidden)
            {
                _skipOnHidden = false;
                _isClosing = false;
                return;
            }

            if (_slots == null || index < 0 || index >= _slots.Length)
            {
                return;
            }
            var item = _slots[index];
            if (item != null)
            {
                SwitchAmmo(item);
            }
            _isClosing = false;
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

            // 仅收集与当前枪口径匹配的子弹类型
            var dict = gun.GunItemSetting.GetBulletTypesInInventory(inventory);
            if (dict == null || dict.Count == 0)
            {
                _slots = new Item[WheelConfig.SLOT_COUNT];
                _wheel.SetSlots(_slots);
                return false;
            }

            var list = new List<Item>();
            foreach (var kv in dict)
            {
                int typeId = kv.Key;
                var rep = FindFirstItemOfType(inventory, typeId);
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

        private static Item FindFirstItemOfType(Inventory inv, int typeId)
        {
            foreach (var item in inv)
            {
                if (item != null && item.TypeID == typeId)
                {
                    return item;
                }
            }
            return null;
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

        private static void LoadCustomSprites()
        {
            if (_slotNormalSprite != null) return;
            try
            {
                string modPath = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location
                );
                string texturePath = System.IO.Path.Combine(modPath, "texture");
                string normalPath = System.IO.Path.Combine(texturePath, "WheelSlot_Normal.png");
                string hoverPath = System.IO.Path.Combine(texturePath, "WheelSlot_Hover.png");
                string selectedPath = System.IO.Path.Combine(texturePath, "WheelSlot_Selected.png");
                _slotNormalSprite = SpriteLoader.LoadFromFile(normalPath, 100f);
                _slotHoverSprite = SpriteLoader.LoadFromFile(hoverPath, 100f);
                _slotSelectedSprite = SpriteLoader.LoadFromFile(selectedPath, 100f);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AmmoWheel] 加载格子贴图失败: {e.Message}");
            }
        }
    }
}
