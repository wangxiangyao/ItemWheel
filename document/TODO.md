# ItemWheel 待办事项清单

*更新时间: 2025-11-07*

---

## 🚀 当前进度总览

- ✅ **已完成**: 基础轮盘系统、长按/短按、9宫格布局
- ⚠️ **进行中**: 轮盘拖拽同步到背包
- 📋 **计划中**: 代码优化、文档完善

---

## ⭐⭐⭐ 最高优先级

### TODO-001: 实现轮盘拖拽同步到背包

**状态**: 🔴 **未开始**

**需求描述**:
当玩家在轮盘上拖拽物品交换位置时，同步更新背包中的物品顺序，保持轮盘布局与背包顺序一致。

**背景**:
- ✅ 当前已实现：背包物品顺序 → 轮盘布局
- ❌ 尚未实现：拖拽轮盘物品 → 改变背包顺序

**参考实现**:
- `../Backpack_QuickWheel/code/src/ShortcutSystem/MainBackpackWheelManager.cs`
  - `OnWheelSlotsSwapped()` (行1247-1261)
  - `AdjustWheelPosition()` (行1271-1374)

**技术方案**:

#### 方案概述
在 `ItemWheelSystem` 中添加双向映射机制，监听 QuickWheel 的槽位交换事件，同步到背包。

#### 实现步骤

**Step 1: 添加映射数据结构** (估计: 30分钟)

在 `ItemWheelSystem.CategoryWheel` 中添加:
```csharp
public class CategoryWheel
{
    // 新增字段
    public int[] WheelToBackpackMapping;     // 轮盘位置 → 背包位置
    public Dictionary<int, int> BackpackToWheelMapping; // 背包位置 → 轮盘位置

    // 构造时初始化
    public CategoryWheel()
    {
        WheelToBackpackMapping = new int[8];
        Array.Fill(WheelToBackpackMapping, -1);
        BackpackToWheelMapping = new Dictionary<int, int>();
    }
}
```

**文件**: `ItemWheelSystem.cs:39-47`

---

**Step 2: 建立初始映射** (估计: 45分钟)

在 `RefreshCategorySlots()` 方法中建立映射关系:

```csharp
private bool RefreshCategorySlots(CategoryWheel wheel)
{
    // ... 现有代码收集物品 ...

    List<Item> collected = CollectItemsForCategory(wheel.Category);

    // 清空旧映射
    Array.Fill(wheel.WheelToBackpackMapping, -1);
    wheel.BackpackToWheelMapping.Clear();

    // 建立新映射
    for (int i = 0; i < collected.Count && i < 8; i++)
    {
        Item item = collected[i];
        int backpackPos = _inventory.Content.IndexOf(item);

        wheel.WheelToBackpackMapping[i] = backpackPos;
        wheel.BackpackToWheelMapping[backpackPos] = i;

        Debug.Log($"[ItemWheel] Mapping: Wheel[{i}] <-> Backpack[{backpackPos}] ({item.DisplayName})");
    }

    // ... 现有代码设置槽位 ...
}
```

**文件**: `ItemWheelSystem.cs:445-498`

---

**Step 3: 在 QuickWheel 中添加事件** (估计: 1小时)

修改 `Wheel.cs` 添加槽位交换事件:

```csharp
// QuickWheel/src/Core/Wheel.cs
public class Wheel<T>
{
    // 新增事件
    public event Action<int, int> OnSlotsSwapped;

    // 在槽位交换时触发（需要找到交换的触发点）
    protected virtual void NotifySlotSwapped(int fromIndex, int toIndex)
    {
        OnSlotsSwapped?.Invoke(fromIndex, toIndex);
    }
}
```

**或者**在 `WheelSlotDisplay.cs` 中触发:

```csharp
// QuickWheel/src/UI/WheelSlotDisplay.cs
private void OnDragEnd(int targetIndex)
{
    // 现有交换逻辑...

    // 新增：通知父级 Wheel
    _parentWheel?.NotifySlotSwapped(_myIndex, targetIndex);
}
```

**文件**:
- `QuickWheel/src/Core/Wheel.cs`
- `QuickWheel/src/UI/WheelSlotDisplay.cs`

---

**Step 4: 监听事件并同步背包** (估计: 1.5小时)

在 `ItemWheelSystem.EnsureWheel()` 中订阅事件:

```csharp
private CategoryWheel EnsureWheel(ItemWheelCategory category)
{
    // ... 现有代码创建轮盘 ...

    Wheel<Item> wheel = new WheelBuilder<Item>()
        // ... 现有配置 ...
        .Build();

    // 新增：订阅槽位交换事件
    wheel.OnSlotsSwapped += (from, to) => OnWheelSlotsSwapped(context, from, to);

    context.Wheel = wheel;
    // ...
}
```

实现交换处理方法:

```csharp
private bool _isPerformingSwap = false; // 防止递归

private void OnWheelSlotsSwapped(CategoryWheel wheel, int fromWheelPos, int toWheelPos)
{
    Debug.Log($"[ItemWheel] Slots swapped: {fromWheelPos} <-> {toWheelPos}");

    // 获取背包位置
    int fromBackpackPos = wheel.WheelToBackpackMapping[fromWheelPos];
    int toBackpackPos = wheel.WheelToBackpackMapping[toWheelPos];

    if (fromBackpackPos == -1)
    {
        Debug.LogWarning($"[ItemWheel] Source position {fromWheelPos} is empty");
        return;
    }

    // 设置标志，防止 onContentChanged 递归触发
    _isPerformingSwap = true;

    try
    {
        if (toBackpackPos != -1)
        {
            // 情况1：目标位置有物品 - 交换背包位置
            var item1 = _inventory.GetItemAt(fromBackpackPos);
            var item2 = _inventory.GetItemAt(toBackpackPos);

            Debug.Log($"[ItemWheel] Swapping backpack positions: {fromBackpackPos} <-> {toBackpackPos}");

            item1.Detach();
            item2.Detach();
            _inventory.AddAt(item2, fromBackpackPos);
            _inventory.AddAt(item1, toBackpackPos);

            // 更新映射（双向交换）
            wheel.WheelToBackpackMapping[fromWheelPos] = toBackpackPos;
            wheel.WheelToBackpackMapping[toWheelPos] = fromBackpackPos;
            wheel.BackpackToWheelMapping[toBackpackPos] = fromWheelPos;
            wheel.BackpackToWheelMapping[fromBackpackPos] = toWheelPos;
        }
        else
        {
            // 情况2：目标位置为空 - 只更新映射，不操作背包
            Debug.Log($"[ItemWheel] Target position is empty, updating mapping only");

            wheel.WheelToBackpackMapping[fromWheelPos] = -1;
            wheel.WheelToBackpackMapping[toWheelPos] = fromBackpackPos;
            wheel.BackpackToWheelMapping[fromBackpackPos] = toWheelPos;
        }
    }
    catch (Exception ex)
    {
        Debug.LogError($"[ItemWheel] Failed to sync backpack: {ex.Message}");
    }
    finally
    {
        _isPerformingSwap = false;
    }
}
```

**文件**: `ItemWheelSystem.cs` (新增方法)

---

**Step 5: 防止递归事件** (估计: 30分钟)

修改 `RefreshCategorySlots()` 检查标志:

```csharp
private bool RefreshCategorySlots(CategoryWheel wheel)
{
    // 在交换过程中跳过刷新
    if (_isPerformingSwap)
    {
        Debug.Log($"[ItemWheel] Swap in progress, skip refresh");
        return true; // 返回true避免错误
    }

    // ... 现有刷新逻辑 ...
}
```

**文件**: `ItemWheelSystem.cs:445`

---

#### 验收标准

- [ ] 拖拽轮盘物品后，打开背包，物品顺序已改变
- [ ] 关闭轮盘，重新打开，新顺序保持
- [ ] 在背包中手动调整顺序，轮盘同步更新
- [ ] 没有崩溃或异常日志
- [ ] 空位交换正常工作
- [ ] 不同类别轮盘互不干扰

#### 测试用例

**测试1: 基本交换**
1. 打开医疗轮盘（假设有3个物品：绷带、急救包、医疗针）
2. 拖拽绷带到急救包位置
3. 关闭轮盘
4. 打开背包，验证物品顺序：急救包、绷带、医疗针

**测试2: 空位交换**
1. 打开轮盘（5个物品）
2. 拖拽位置1的物品到空位置6
3. 验证位置1变空，位置6有物品

**测试3: 跨类别不干扰**
1. 调整医疗轮盘物品顺序
2. 打开刺激剂轮盘，验证顺序未受影响
3. 在背包中调整刺激剂物品
4. 重新打开医疗轮盘，验证医疗物品顺序保持

**测试4: 递归防护**
1. 启用详细日志
2. 执行多次快速拖拽交换
3. 检查日志，确认没有递归调用警告

---

#### 预计工作量

- **总时间**: 4-5小时
- **难度**: 🔥🔥🔥 中高
- **风险**:
  - ⚠️ QuickWheel 可能没有暴露拖拽事件，需要修改源码
  - ⚠️ 递归事件可能导致死循环，需要仔细测试

---

## ⭐⭐ 中等优先级

### TODO-002: 完善 QuickWheel 事件系统

**状态**: 🟡 **依赖 TODO-001**

**需求描述**:
QuickWheel 当前可能没有完整暴露槽位交换事件，需要在框架层面完善事件系统。

**实现建议**:

在 `Wheel.cs` 中添加:
```csharp
public event Action<int, int> OnSlotsSwapped;
public event Action<int> OnSlotRemoved;
public event Action<int, T> OnSlotUpdated;
```

在 `WheelSlotDisplay.cs` 中触发:
```csharp
protected void TriggerSlotSwapped(int fromIndex, int toIndex)
{
    _parentWheel.OnSlotsSwapped?.Invoke(fromIndex, toIndex);
}
```

**文件**:
- `QuickWheel/src/Core/Wheel.cs`
- `QuickWheel/src/UI/WheelSlotDisplay.cs`

**预计工作量**: 1-2小时

---

### TODO-003: 支持背包物品增删时自动更新轮盘

**状态**: 🔴 **未开始**

**需求描述**:
当玩家在背包中添加或删除物品时，如果轮盘正在显示，自动刷新轮盘内容。

**当前问题**:
- 轮盘显示后，修改背包物品
- 关闭轮盘再次打开，可能显示旧数据

**实现建议**:

监听 `Inventory.onContentChanged` 事件:
```csharp
private void OnInventoryChanged(Inventory inventory, int changedSlot)
{
    if (_isPerformingSwap) return; // 跳过自己触发的变化

    // 检查哪个类别受影响
    ItemWheelCategory affectedCategory = DetermineCategory(changedSlot);

    if (affectedCategory != ItemWheelCategory.None)
    {
        // 如果该类别轮盘正在显示，刷新
        if (_wheels.TryGetValue(affectedCategory, out var wheel))
        {
            if (wheel.Wheel.IsVisible)
            {
                RefreshCategorySlots(wheel);
                // 通知UI刷新
                wheel.View?.Refresh();
            }
        }
    }
}
```

**文件**: `ItemWheelSystem.cs` (新增方法)

**预计工作量**: 2-3小时

---

### TODO-004: 添加轮盘物品数量显示

**状态**: 🔴 **未开始**

**需求描述**:
在轮盘格子上显示物品堆叠数量（例如：绷带 x3）

**参考**: 游戏原生 `ItemDisplay` 有数量显示

**实现位置**: `WheelSlotDisplay.cs`

**预计工作量**: 1小时

---

## ⭐ 低优先级

### TODO-005: 减少Debug日志输出

**状态**: 🔴 **未开始**

**需求描述**:
当前有大量 Debug.Log，影响性能和可读性，生产环境不需要。

**实现方案**:

方案1: 条件编译
```csharp
#if DEBUG
    Debug.Log("[ItemWheel] ...");
#endif
```

方案2: 日志等级
```csharp
public static class WheelLog
{
    public static LogLevel Level = LogLevel.Warning;

    public static void Info(string msg)
    {
        if (Level <= LogLevel.Info) Debug.Log(msg);
    }
}
```

**预计工作量**: 1小时

---

### TODO-006: 提取配置常量

**状态**: 🔴 **未开始**

**需求描述**:
将魔法数字提取到配置类。

**当前问题**:
- 长按阈值 `0.15f` 硬编码在代码中
- 格子大小 `90f` 硬编码
- 间距 `12f` 硬编码

**实现方案**:

创建 `WheelConfig.cs`:
```csharp
public static class WheelConfig
{
    public const int SLOT_COUNT = 9;
    public const float LONG_PRESS_THRESHOLD = 0.15f;
    public const float GRID_CELL_SIZE = 90f;
    public const float GRID_SPACING = 12f;

    // 轮盘位置映射
    public static readonly Vector2Int[] GRID_POSITIONS = ...;
}
```

**文件**: 新建 `WheelConfig.cs`

**预计工作量**: 30分钟

---

### TODO-007: 编写单元测试

**状态**: 🔴 **未开始**

**需求描述**:
为关键逻辑添加单元测试。

**测试范围**:
- 双向映射正确性
- 长按/短按检测
- 物品分类逻辑

**框架**: Unity Test Framework 或 NUnit

**预计工作量**: 3-4小时

---

### TODO-008: 添加XML文档注释

**状态**: 🔴 **未开始**

**需求描述**:
为公共API添加XML注释，方便IDE智能提示。

**示例**:
```csharp
/// <summary>
/// 显示指定类别的轮盘
/// </summary>
/// <param name="category">物品类别</param>
/// <param name="wheelCenter">轮盘中心位置（可选）</param>
/// <returns>是否成功显示</returns>
public bool ShowWheel(ItemWheelCategory category, Vector2? wheelCenter = null)
```

**预计工作量**: 2小时

---

### TODO-009: 绘制完整架构图

**状态**: 🔴 **未开始**

**需求描述**:
使用 draw.io 绘制完整架构图，包括：
- 类关系图
- 数据流图
- 事件流图

**参考**: `document/QuickWheel架构图.drawio`

**预计工作量**: 2小时

---

## 🐛 已知问题

### BUG-001: Y轴坐标系反转

**状态**: ✅ **已修复**

**问题**: 鼠标移动到上边选择了下边

**根因**: Unity坐标系Y向上为正，屏幕坐标系Y向下为正

**修复**: `Vector2 correctedDirection = new Vector2(direction.x, -direction.y);`

**文件**: `QuickWheel/src/Selection/GridSelectionStrategy.cs`

---

### BUG-002: 物品图标不显示

**状态**: ✅ **已修复**

**问题**: WheelSlotDisplay 只在初始化时有数据才创建图标对象

**修复**: 总是创建图标和标签对象，用 SetActive() 控制显示

**文件**: `QuickWheel/src/UI/WheelSlotDisplay.cs`

---

## 📊 进度追踪

### 功能完成度

| 功能模块 | 状态 | 完成度 |
|---------|------|--------|
| 快捷键拦截 | ✅ 完成 | 100% |
| 长按/短按检测 | ✅ 完成 | 100% |
| 9宫格布局 | ✅ 完成 | 100% |
| 自定义Sprite | ✅ 完成 | 100% |
| 物品图标显示 | ✅ 完成 | 100% |
| 背包→轮盘同步 | ✅ 完成 | 100% |
| **轮盘→背包同步** | 🔴 **未开始** | **0%** |
| 实时刷新 | ⚠️ 部分完成 | 70% |
| 错误处理 | ⚠️ 部分完成 | 60% |
| 单元测试 | 🔴 未开始 | 0% |
| 文档完善 | ⚠️ 进行中 | 80% |

---

## 🎯 里程碑

### Milestone 1: 基础功能 ✅

- [x] 快捷键拦截
- [x] 长按/短按检测
- [x] 9宫格布局
- [x] 物品显示
- [x] 物品使用

**完成日期**: 2025-11-06

---

### Milestone 2: 核心功能 ⚠️ (进行中)

- [x] 背包物品→轮盘布局
- [ ] **轮盘拖拽→背包同步** ← 当前关键任务
- [ ] 实时刷新机制
- [ ] 边界情况处理

**目标日期**: 2025-11-08

---

### Milestone 3: 优化与测试 🔜

- [ ] 单元测试覆盖
- [ ] 性能优化
- [ ] 日志优化
- [ ] 代码重构

**目标日期**: 2025-11-10

---

### Milestone 4: 发布准备 🔜

- [ ] 文档完善
- [ ] 使用手册
- [ ] 发布测试
- [ ] 用户反馈收集

**目标日期**: 2025-11-12

---

## 📞 联系与协作

如需讨论任何TODO项，请：
1. 在此文档中添加评论
2. 创建 GitHub Issue（如有）
3. 更新进度状态

---

*最后更新: 2025-11-07 by Claude*
