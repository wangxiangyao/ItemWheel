# ItemWheel ModSetting 集成指南

## 📖 概述

本指南说明如何将ItemWheel与ModSetting框架集成，实现可配置的设置面板。

### ✅ 已完成的工作

已经实现了以下组件：

1. **ModSettingFacade.cs** - ModSetting兼容层
   - 自动检测ModSetting是否可用
   - 可用时使用ModSetting配置，不可用则使用默认配置
   - 提供统一的设置访问接口
   - 在ModSetting中注册完整的UI控件

2. **ItemWheelModSettings.cs** - 设置数据模型
   - 包含所有可配置项
   - 提供默认值确保向后兼容
   - 支持搜索设置、轮盘开关、特殊功能等

3. **InventorySearcher.cs** - 多Inventory搜索器
   - 支持搜索主背包和宠物背包
   - 支持搜索容器内的物品
   - 返回结果包含来源信息（主背包/宠物背包/容器）

## 🏗️ 架构设计

```
ItemWheelSystem/AmmoWheelSystem
        ↓
    ModSettingFacade (统一接口)
        ↓
    ┌───────────────┴───────────────┐
    ↓                               ↓
ModSetting可用                   ModSetting不可用
    ↓                               ↓
从ModSetting读取设置           使用默认配置
自动生成UI面板                 无配置面板
自动持久化                     无需持久化
```

## 📝 需要集成的位置

### 1. 在 ModBehaviour.cs 中初始化

在 `ModBehaviour.cs` 的 `Awake()` 或 `Start()` 方法中添加：

```csharp
using ItemWheel;

private void Awake()
{
    // 初始化ModSettingFacade
    // 这会自动检测ModSetting是否可用
    ModSettingFacade.Initialize(ModInfo);

    // 现在可以通过 ModSettingFacade.Settings 访问配置
    bool searchInSlots = ModSettingFacade.Settings.SearchInSlots;
    bool searchInPet = ModSettingFacade.Settings.SearchInPetInventory;

    // ... 其他初始化代码 ...
}
```

### 2. 在 ItemWheelSystem.cs 中集成搜索

修改 `CollectItemsForCategory` 方法：

```csharp
private List<CollectedItemInfo> CollectItemsForCategory(ItemWheelCategory category)
{
    var results = new List<CollectedItemInfo>();
    var addedItems = new HashSet<Item>();

    if (_inventory?.Content == null)
        return results;

    // 获取要搜索的背包列表（根据设置决定是否包含宠物背包）
    var inventories = InventorySearcher.GetInventoriesToSearch(
        _inventory,
        ModSettingFacade.Settings
    );

    // 检查是否启用了该类别的轮盘
    if (!ModSettingFacade.Settings.IsWheelEnabled(category))
    {
        Debug.Log($"[ItemWheel] {category} 轮盘已禁用");
        return results;
    }

    // 使用通用搜索器搜索物品
    bool searchInSlots = ModSettingFacade.Settings.SearchInSlots;
    var searchResults = InventorySearcher.SearchAll(
        inventories,
        item => MatchesCategory(item, category),
        searchInSlots
    );

    // 转换搜索结果为CollectedItemInfo格式
    foreach (var result in searchResults)
    {
        if (addedItems.Contains(result.Item))
            continue;

        results.Add(new CollectedItemInfo(
            result.Item,
            result.IsFromSlot,
            result.BackpackIndex
        ));
        addedItems.Add(result.Item);

        if (results.Count >= WheelConfig.SLOT_COUNT - 1)
            break;
    }

    return results;
}
```

### 3. 在 AmmoWheelSystem.cs 中集成搜索

修改 `RefreshSlots` 方法中的搜索逻辑：

```csharp
private bool RefreshSlots()
{
    _typeToItem.Clear();

    var character = CharacterMainControl.Main;
    var gun = character?.GetGun();
    var inventory = character?.CharacterItem?.Inventory;

    if (gun == null || gun.GunItemSetting == null || inventory == null)
        return false;

    // 检查是否启用了子弹轮盘
    if (!ModSettingFacade.Settings.EnableAmmoWheel)
    {
        Debug.Log("[AmmoWheel] 子弹轮盘已禁用");
        return false;
    }

    // 获取可用的背包列表（包括宠物背包，如果启用）
    var inventories = InventorySearcher.GetInventoriesToSearch(
        inventory,
        ModSettingFacade.Settings
    );

    bool searchInSlots = ModSettingFacade.Settings.SearchInSlots;

    // 搜索所有匹配的子弹（从多个背包）
    // 修改 FindFirstItemOfType 为支持多Inventory的版本
    // ...

    return true;
}

// 修改 FindFirstItemOfType 以支持多Inventory
private static Item FindFirstItemOfType(
    IEnumerable<Inventory> inventories,
    int typeId,
    bool searchInSlots)
{
    var result = InventorySearcher.FindFirst(
        inventories,
        item => item != null && item.TypeID == typeId,
        searchInSlots
    );

    return result?.Item;
}
```

### 4. 在 ShowWheel 中检查轮盘是否启用

```csharp
public bool ShowWheel(ItemWheelCategory category, Vector2? wheelCenter = null)
{
    // 检查该类别轮盘是否启用
    if (!ModSettingFacade.Settings.IsWheelEnabled(category))
    {
        Debug.Log($"[ItemWheel] {category} 轮盘已禁用，不显示");
        return false;
    }

    // 原有逻辑...
}
```

### 5. 检查并禁用拖拽功能

在 `RefreshCategorySlots` 中添加拖拽禁用逻辑：

```csharp
private bool RefreshCategorySlots(CategoryWheel wheel, bool resetSelection = true)
{
    // ... 搜索物品 ...

    // 检查是否有宠物背包物品
    bool hasPetItems = collected.Any(info => info.IsFromPet);

    // 如果有宠物背包物品，重新创建轮盘并禁用拖拽
    if (hasPetItems && wheel.Wheel != null)
    {
        Debug.Log($"[ItemWheel] 检测到宠物背包物品，禁用拖拽功能");

        // 保存当前状态
        var lastIndex = wheel.LastConfirmedIndex;

        // 重新创建轮盘（设置 EnableDragSwap = false）
        RecreateWheelWithDragDisabled(wheel);

        wheel.LastConfirmedIndex = lastIndex;
    }

    // ... 其他逻辑 ...
}

private void RecreateWheelWithDragDisabled(CategoryWheel wheel)
{
    // 重新创建Wheel对象，设置 EnableDragSwap = false
    // 使用 WheelBuilder 重新创建
    // 参考 EnsureWheel 方法，但修改 config.EnableDragSwap = false
}
```

## ⚙️ 配置项说明

### 搜索设置
- **SearchInSlots** - 是否搜索容器内的物品（默认：true）
- **SearchInPetInventory** - 是否搜索宠物背包（默认：true）

### 轮盘类别开关
- **EnableMedicalWheel** - 医疗品轮盘（快捷键3）
- **EnableStimWheel** - 刺激物轮盘（快捷键4）
- **EnableFoodWheel** - 食物轮盘（快捷键5）
- **EnableExplosiveWheel** - 手雷轮盘（快捷键6）
- **EnableMeleeWheel** - 近战武器轮盘（快捷键V）
- **EnableAmmoWheel** - 子弹轮盘（长按R）

### UI设置
- **ShowItemCount** - 显示物品数量
- **ShowDurabilityBar** - 显示耐久条

## 🎯 使用示例

### 检查设置并执行逻辑

```csharp
// 检查是否启用了某功能
if (ModSettingFacade.Settings.SearchInPetInventory)
{
    // 搜索宠物背包
}

// 检查轮盘是否启用
if (ModSettingFacade.Settings.IsWheelEnabled(ItemWheelCategory.Medical))
{
    // 注册医疗品轮盘快捷键
}

// 获取搜索配置
var inventories = InventorySearcher.GetInventoriesToSearch(
    _inventory,
    ModSettingFacade.Settings
);
```

## 🔧 依赖声明

在创意工坊发布时，声明ModSetting为可选依赖：

```json
{
  "name": "ItemWheel",
  "version": "1.0.0",
  "dependencies": {
    "ModSetting": "*"
  },
  "optionalDependencies": {
    "ModSetting": "ModSetting框架提供图形化配置面板"
  }
}
```

## ✅ 测试清单

- [ ] 未安装ModSetting时，功能正常（使用默认配置）
- [ ] 安装ModSetting后，自动显示配置面板
- [ ] 修改设置后即时生效
- [ ] 重启游戏后设置保持
- [ ] 禁用某轮盘后，快捷键不响应
- [ ] 启用宠物背包搜索后，能搜索到宠物背包物品
- [ ] 禁用容器搜索后，不搜索容器内物品
- [ ] 有宠物背包物品时，轮盘拖拽功能被禁用

## 📚 参考文档

- ModSetting框架路径：`D:\02_projects\Mod\Duckov\Gamesource\ModSetting\`
- ModSettingAPI.cs - 主要API接口
- ModConfig.cs - 配置管理类
