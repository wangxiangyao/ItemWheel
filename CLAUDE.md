# ItemWheel 项目记忆

## 项目基本信息
- **项目路径**: `D:\02_projects\Mod\Duckov\ItemWheel\`
- **目标框架**: .NET Standard 2.1
- **主程序集**: ItemWheel
- **根命名空间**: ItemWheel

## 关键配置

### csproj 关键配置
```xml
<DuckovPath>D:\steam\steamapps\common\Escape from Duckov</DuckovPath>
<OutputPath>$(DuckovPath)\Duckov_Data\Mods\ItemWheel</OutputPath>
```

### QuickWheel 集成
- **路径**: `../QuickWheel/src/**/*.cs` (相对路径)
- **方式**: 直接包含源码编译到 ItemWheel.dll
- **不是NuGet包**: 内嵌到模组中

## 项目结构
```
ItemWheel/
├── ItemWheelSystem.cs        # 主要轮盘系统
├── ItemWheelAdapter.cs       # 物品适配器
├── ModBehaviour.cs           # 模组入口
└── GameSource/               # 游戏源码参考(不编译)

QuickWheel/src/
├── Core/Wheel.cs             # 轮盘主类
├── Selection/GridSelectionStrategy.cs  # 9宫格选择策略
├── Input/MouseWheelInput.cs  # 鼠标输入处理
└── Interfaces/               # 接口定义
```

## 当前问题

### 主要Bug: 上下选择反转
- **现象**: 鼠标移动到上边选择了下边
- **根本原因**: Y轴坐标系方向问题
- **问题位置**: `GridSelectionStrategy.cs` 的 `GetDirectionIndexFromAngle` 函数

### 9宫格布局
```
[7] [2] [6]    左上 上中 右上
[0] [ ] [1]    左中 中心 右中
[4] [3] [5]    左下 下中 右下
```

### 角度映射 (Unity坐标系)
- **2**: 上中 (247.5° - 292.5°)
- **3**: 下中 (67.5° - 112.5°)
- **上下反转**: Unity的Y轴向上为正，与屏幕坐标相反

## 架构问题

### QuickWheel设计不完整
- `Wheel.HandleInputPositionChanged` 方法内部为空
- 有InputHandler和SelectionStrategy接口但没真正使用
- ItemWheel绕过了架构，直接调用`Wheel.UpdateInput()`

### ItemWheel当前实现
- `ItemWheelSystem.Update()` 直接调用 `_activeWheel.Wheel.UpdateInput()`
- 没有使用默认的 `MouseWheelInput`
- 选择策略调用链不完整

## ✅ 已完成的修复

### 1. **完善QuickWheel架构**
- ✅ 重新设计 `IWheelSelectionStrategy` 接口，使用 `WheelSelectionContext`
- ✅ 完善 `HandleInputPositionChanged` 方法，调用 SelectionStrategy
- ✅ 为 `IWheelView` 添加 `GetWheelCenter()` 方法
- ✅ `DefaultWheelView` 实现 `GetWheelCenter()` 方法

### 2. **修复GridSelectionStrategy上下反转**
- ✅ **问题根因**: Unity坐标系Y向上为正，屏幕坐标系Y向下为正
- ✅ **修复方案**: 在计算角度前将Y轴取反
```csharp
Vector2 correctedDirection = new Vector2(direction.x, -direction.y);
```

### 3. **ItemWheel使用默认架构**
- ✅ 移除自定义输入处理逻辑
- ✅ 使用 `MouseWheelInput` 作为默认输入处理器
- ✅ 配置不同类别使用不同触发键（3-6键）
- ✅ 简化 `Update()` 方法，只调用 `Wheel.Update()`

### 4. **修复物品图标不显示问题**
- ✅ **问题根因**: `WheelSlotDisplay` 只在初始化时有数据才创建图标对象
- ✅ **修复方案**: 总是创建图标和标签对象，用 `SetActive()` 控制显示

### 5. **自定义格子Sprite支持**
- ✅ 创建 `SpriteLoader` 工具类从文件系统加载PNG
- ✅ 在 `WheelConfig` 中添加三个Sprite字段（Normal/Hover/Selected）
- ✅ `WheelSlotDisplay` 支持使用自定义Sprite或默认纯色
- ✅ `ItemWheelSystem` 自动加载 `texture/` 目录下的PNG文件
- ✅ 轮盘背景完全透明（`Color.clear`）

## 当前架构状态
✅ **QuickWheel架构完整**: InputHandler → SelectionStrategy → View
✅ **ItemWheel遵循架构**: 使用默认组件，专注业务逻辑
✅ **上下选择修复**: Y轴坐标系问题已解决

## 开发环境
- Unity游戏模组开发
- 使用Harmony进行游戏钩子
- 输出到Duckov游戏Mods文件夹