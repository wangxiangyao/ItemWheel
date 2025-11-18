using System;
using System.Collections.Generic;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using ItemWheel.Core.ItemSources;
using ItemWheel.Data;
using ItemWheel.Integration;
using ItemWheel.UI;
using ItemWheel.Utils;
using QuickWheel.Core;
using QuickWheel.Selection;
using QuickWheel.UI;
using QuickWheel.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ItemWheel.Features.TotemWheel
{
    internal sealed class TotemWheelSystem
    {
        private const int TotemSlotCount = 2;
        private const int WheelSlotCount = WheelConfig.SLOT_COUNT;
        private const float HoldThreshold = 0.2f;
        private const string TotemTagName = "Totem";

        private readonly KeyState _state = new KeyState();
        private Wheel<Item> _wheel;
        private DefaultWheelView<Item> _view;
        private QuickWheel.Input.MouseWheelInput _input;
        private Item[] _slots = new Item[WheelSlotCount];
        private readonly Dictionary<int, ItemLocation> _inventoryLocations = new Dictionary<int, ItemLocation>();
        private readonly Dictionary<int, Slot> _slotMapping = new Dictionary<int, Slot>();
        private readonly List<Slot> _detectedSlots = new List<Slot>(TotemSlotCount);
        private readonly HashSet<Item> _equippedItems = new HashSet<Item>();
        private KeyCode _lastLegacyKey = KeyCode.None;
        private bool _legacyKeyPressed;

        private sealed class KeyState
        {
            public bool IsPressed;
            public float HoldTime;
            public bool HasTriggeredWheel;
            public Vector2 PressedMousePosition;
        }

        public bool HasActiveWheel => _wheel?.IsVisible == true;

        public void Update()
        {
            var settings = ModSettingFacade.Settings;
            if (settings == null || !settings.EnableTotemWheel)
            {
                if (HasActiveWheel)
                {
                    HideWheel();
                }

                ResetPressState();
                return;
            }

            var activationKey = settings.TotemWheelKey;
            ReadKeyState(activationKey, out bool isPressed, out bool pressedThisFrame, out bool releasedThisFrame);

            if (pressedThisFrame)
            {
                BeginPress();
            }

            if (releasedThisFrame)
            {
                EndPress();
            }

            if (_state.IsPressed && !_state.HasTriggeredWheel)
            {
                _state.HoldTime += Time.unscaledDeltaTime;
                if (_state.HoldTime >= HoldThreshold)
                {
                    if (ShowWheel(_state.PressedMousePosition))
                    {
                        _state.HasTriggeredWheel = true;
                    }
                    else
                    {
                        ResetPressState();
                    }
                }
            }

            if (HasActiveWheel)
            {
                _wheel?.Update();
            }
        }

        private void BeginPress()
        {
            if (UIFocusDetector.IsInputFieldFocused())
            {
                return;
            }

            if (CharacterMainControl.Main == null)
            {
                return;
            }

            _state.IsPressed = true;
            _state.HoldTime = 0f;
            _state.HasTriggeredWheel = false;
            _state.PressedMousePosition = UnityEngine.Input.mousePosition;
        }

        private void EndPress()
        {
            if (!_state.IsPressed)
            {
                return;
            }

            if (_state.HasTriggeredWheel)
            {
                HideWheel();
            }

            ResetPressState();
        }

        private void ResetPressState()
        {
            _state.IsPressed = false;
            _state.HoldTime = 0f;
            _state.HasTriggeredWheel = false;
        }

        private bool ShowWheel(Vector2 center)
        {
            if (CharacterMainControl.Main == null)
            {
                return false;
            }

            EnsureWheel();

            if (!RefreshSlots())
            {
                BubbleNotifier.Show("没有可用的图腾");
                return false;
            }

            _view.SetWheelCenterBeforeShow(center);
            int preferredIndex = GetPreferredIndex();
            if (preferredIndex >= 0)
            {
                _wheel.SetSelectedIndex(preferredIndex);
            }

            _wheel.Show();
            return true;
        }

        private void HideWheel()
        {
            if (_wheel?.IsVisible == true)
            {
                _wheel.Hide(true);
            }
        }

        private void EnsureWheel()
        {
            if (_wheel != null)
            {
                return;
            }

            WheelSpriteLoader.Load();

            _input = new QuickWheel.Input.MouseWheelInput();
            _view = new DefaultWheelView<Item>();

            _wheel = new WheelBuilder<Item>()
                .WithConfig(cfg =>
                {
                    cfg.EnablePersistence = false;
                    cfg.GridCellSize = 90f;
                    cfg.GridSpacing = 12f;
                    cfg.DeadZoneRadius = 40f;
                    cfg.EnableClickSelect = false;
                    cfg.SlotNormalSprite = WheelSpriteLoader.SlotNormal;
                    cfg.SlotHoverSprite = WheelSpriteLoader.SlotHover;
                    cfg.SlotSelectedSprite = WheelSpriteLoader.SlotSelected;
                    cfg.CanDragSlot = CanDragSlot;
                })
                .WithAdapter(new SimpleItemAdapter())
                .WithView(_view)
                .WithInput(_input)
                .WithSelectionStrategy(new GridSelectionStrategy())
                .OnWheelShown(WheelInputGuard.OnWheelShown)
                .OnWheelHidden(index =>
                {
                    WheelInputGuard.OnWheelHidden();
                    ResetPressState();
                })
                .Build();

            _wheel.EventBus.OnSlotsSwapped += HandleSlotsSwapped;

            _slots = new Item[WheelSlotCount];
            _wheel.SetSlots(_slots);
        }

        private bool RefreshSlots()
        {
            var character = CharacterMainControl.Main;
            var inventory = character?.CharacterItem?.Inventory;
            if (character == null || inventory == null)
            {
                return false;
            }

            _inventoryLocations.Clear();
            _slotMapping.Clear();
            _detectedSlots.Clear();
            _equippedItems.Clear();

            var slotCollection = character.CharacterItem?.Slots;
            if (slotCollection != null)
            {
                foreach (var slot in slotCollection)
                {
                    if (slot == null)
                    {
                        continue;
                    }

                    if (SlotRequiresTotem(slot))
                    {
                        _detectedSlots.Add(slot);
                        if (_detectedSlots.Count >= TotemSlotCount)
                        {
                            break;
                        }
                    }
                }
            }

            if (_detectedSlots.Count == 0)
            {
                return false;
            }

            var newSlots = new Item[WheelSlotCount];
            bool hasAnyItem = false;

            for (int i = 0; i < TotemSlotCount; i++)
            {
                var slot = i < _detectedSlots.Count ? _detectedSlots[i] : null;
                _slotMapping[i] = slot;
                var content = slot?.Content;
                newSlots[i] = content;
                if (content != null)
                {
                    _equippedItems.Add(content);
                    hasAnyItem = true;
                }
            }

            var settings = ModSettingFacade.Settings;
            var inventories = InventorySearcher.GetInventoriesToSearch(inventory, settings.SearchInPetInventory);
            var options = new InventorySearchOptions(inventories, IsTotemItem, settings, character);
            var searchResults = InventorySearcher.SearchAll(options);

            int wheelIndex = TotemSlotCount;
            foreach (var result in searchResults)
            {
                if (result?.Item == null)
                {
                    continue;
                }

                if (_equippedItems.Contains(result.Item))
                {
                    continue;
                }

                if (wheelIndex == 8)
                {
                    wheelIndex++;
                }

                if (wheelIndex >= WheelSlotCount)
                {
                    break;
                }

                newSlots[wheelIndex] = result.Item;
                _inventoryLocations[wheelIndex] = CreateLocation(result);
                hasAnyItem = true;
                wheelIndex++;
            }

            _slots = newSlots;
            _wheel?.SetSlots(_slots);
            return hasAnyItem;
        }

        private int GetPreferredIndex()
        {
            for (int i = 0; i < TotemSlotCount; i++)
            {
                if (_slots != null && i < _slots.Length && _slots[i] != null)
                {
                    return i;
                }
            }

            if (_slots == null)
            {
                return -1;
            }

            for (int i = TotemSlotCount; i < _slots.Length; i++)
            {
                if (i == 8)
                {
                    continue;
                }

                if (_slots[i] != null)
                {
                    return i;
                }
            }

            return -1;
        }

        private void HandleSlotsSwapped(int index1, int index2)
        {
            if (_slots == null)
            {
                RefreshSlots();
                return;
            }

            if (!IsValidIndex(index1) || !IsValidIndex(index2))
            {
                RefreshSlots();
                return;
            }

            bool index1IsEquipped = IsTotemSlotIndex(index1);
            bool index2IsEquipped = IsTotemSlotIndex(index2);

            if (!index1IsEquipped && !index2IsEquipped)
            {
                RefreshSlots();
                return;
            }

            if (index1IsEquipped && index2IsEquipped)
            {
                if (!TrySwapEquippedSlots(index1, index2))
                {
                    Debug.LogWarning("[TotemWheel] 切换装备槽失败");
                }

                RefreshSlots();
                return;
            }

            int equippedIndex = index1IsEquipped ? index1 : index2;
            int inventoryIndex = index1IsEquipped ? index2 : index1;

            if (!_slotMapping.TryGetValue(equippedIndex, out var targetSlot) || targetSlot == null)
            {
                RefreshSlots();
                return;
            }

            if (!_inventoryLocations.TryGetValue(inventoryIndex, out var location))
            {
                RefreshSlots();
                return;
            }

            var inventoryItem = _slots[inventoryIndex];
            if (inventoryItem == null)
            {
                RefreshSlots();
                return;
            }

            var character = CharacterMainControl.Main;
            if (!SwapWithTotemSlot(targetSlot, inventoryItem, location, character))
            {
                Debug.LogWarning("[TotemWheel] 图腾交换失败");
                RefreshSlots();
                return;
            }

            RefreshSlots();
        }

        private bool TrySwapEquippedSlots(int indexA, int indexB)
        {
            if (!_slotMapping.TryGetValue(indexA, out var slotA) || !_slotMapping.TryGetValue(indexB, out var slotB))
            {
                return false;
            }

            if (slotA == null && slotB == null)
            {
                return false;
            }

            var itemA = slotA?.Content;
            var itemB = slotB?.Content;

            if (itemA == itemB)
            {
                return false;
            }

            try
            {
                if (itemA == null)
                {
                    return itemB != null && slotA != null && slotA.Plug(itemB, out _);
                }

                if (itemB == null)
                {
                    return slotB != null && slotB.Plug(itemA, out _);
                }

                if (!slotA.Plug(itemB, out var displaced))
                {
                    return false;
                }

                if (slotB == null)
                {
                    return false;
                }

                return slotB.Plug(displaced, out _);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TotemWheel] 交换图腾槽失败: {ex.Message}");
                return false;
            }
        }

        private bool SwapWithTotemSlot(Slot targetSlot, Item inventoryItem, ItemLocation location, CharacterMainControl character)
        {
            if (targetSlot == null || inventoryItem == null || character == null)
            {
                return false;
            }

            try
            {
                if (targetSlot.Content == inventoryItem)
                {
                    return true;
                }

                if (!targetSlot.Plug(inventoryItem, out var removed))
                {
                    return false;
                }

                if (removed != null && removed != inventoryItem)
                {
                    if (!PlaceItemBack(removed, location, character))
                    {
                        TryAddToMainInventory(removed, character);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TotemWheel] 插入图腾槽失败: {ex.Message}");
                return false;
            }
        }

        private (bool canDrag, string reason) CanDragSlot(int slotIndex)
        {
            if (!IsValidIndex(slotIndex))
            {
                return (false, "无效槽位");
            }

            if (_slots == null || slotIndex >= _slots.Length || _slots[slotIndex] == null)
            {
                return (false, "空槽位");
            }

            if (!IsTotemSlotIndex(slotIndex) && !_inventoryLocations.ContainsKey(slotIndex))
            {
                return (false, "来源未知");
            }

            return (true, null);
        }

        private bool PlaceItemBack(Item item, ItemLocation location, CharacterMainControl character)
        {
            if (item == null)
            {
                return false;
            }

            if (location.Inventory != null)
            {
                try
                {
                    if (location.IsFromSlot && location.BackpackIndex >= 0 && location.BackpackIndex < location.Inventory.Content.Count)
                    {
                        var container = location.Inventory.Content[location.BackpackIndex];
                        var slots = container?.Slots;
                        var targetSlot = slots != null && location.SlotIndex >= 0 && location.SlotIndex < slots.Count
                            ? slots.GetSlotByIndex(location.SlotIndex)
                            : null;

                        if (targetSlot != null)
                        {
                            item.Detach();
                            if (targetSlot.Plug(item, out var displaced))
                            {
                                if (displaced != null && displaced != item)
                                {
                                    TryAddToMainInventory(displaced, character);
                                }
                                return true;
                            }
                        }
                    }

                    item.Detach();
                    if (location.BackpackIndex >= 0 &&
                        location.BackpackIndex < location.Inventory.Content.Count &&
                        location.Inventory.Content[location.BackpackIndex] == null &&
                        location.Inventory.AddAt(item, location.BackpackIndex))
                    {
                        return true;
                    }

                    if (location.Inventory.AddItem(item))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[TotemWheel] 放回物品失败: {ex.Message}");
                }
            }

            return TryAddToMainInventory(item, character);
        }

        private bool TryAddToMainInventory(Item item, CharacterMainControl character)
        {
            var inventory = character?.CharacterItem?.Inventory;
            if (inventory == null)
            {
                return false;
            }

            try
            {
                item.Detach();
                return inventory.AddItem(item);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TotemWheel] 回收物品失败: {ex.Message}");
                return false;
            }
        }

        private void ReadKeyState(KeyCode keyCode, out bool isPressed, out bool pressedThisFrame, out bool releasedThisFrame)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && TryConvertKey(keyCode, out var inputKey))
            {
                var control = keyboard[inputKey];
                if (control != null)
                {
                    isPressed = control.isPressed;
                    pressedThisFrame = control.wasPressedThisFrame;
                    releasedThisFrame = control.wasReleasedThisFrame;
                    return;
                }
            }

            if (_lastLegacyKey != keyCode)
            {
                _legacyKeyPressed = false;
                _lastLegacyKey = keyCode;
            }

            bool current = UnityEngine.Input.GetKey(keyCode);
            isPressed = current;
            pressedThisFrame = current && !_legacyKeyPressed;
            releasedThisFrame = !current && _legacyKeyPressed;
            _legacyKeyPressed = current;
        }

        private static bool TryConvertKey(KeyCode keyCode, out Key key)
        {
            if (Enum.TryParse(keyCode.ToString(), out key))
            {
                return true;
            }

            key = Key.None;
            return false;
        }

        private static ItemLocation CreateLocation(SearchResult result)
        {
            return new ItemLocation(result.Source, result.BackpackIndex, result.SlotIndex);
        }

        private static bool IsTotemItem(Item item)
        {
            if (item?.Tags == null)
            {
                return false;
            }

            foreach (var tag in item.Tags)
            {
                if (tag != null && string.Equals(tag.name, TotemTagName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SlotRequiresTotem(Slot slot)
        {
            if (slot?.requireTags == null)
            {
                return false;
            }

            foreach (var tag in slot.requireTags)
            {
                if (tag != null && string.Equals(tag.name, TotemTagName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTotemSlotIndex(int index) => index >= 0 && index < TotemSlotCount;

        private static bool IsValidIndex(int index) => index >= 0 && index < WheelSlotCount && index != 8;
    }
}
