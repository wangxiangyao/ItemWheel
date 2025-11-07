# Duckov Mod 程序集加载问题记录

## 问题描述

在开发 ItemWheel Mod 时，遇到了持续的 `TypeLoadException`，导致 Mod 无法加载。

## 错误症状

```
TypeLoadException: Could not load type of field 'ItemWheel.ItemWheelSystem+CategoryWheel:Wheel' (1)
due to: Could not load file or assembly 'QuickWheel, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'
or one of its dependencies.
```

**关键特征**：
- 错误发生在类型加载阶段（Update() 方法调用之前）
- 提示找不到 QuickWheel.dll 程序集
- QuickWheel.dll 确实存在于 Mod 目录中
- 问题与字段类型（`Wheel<Item>`）有关

## 项目背景

**初始架构**：
- **QuickWheel**: 通用轮盘选择模块（独立 DLL）
- **ItemWheel**: 物品轮盘 Mod（独立 DLL，引用 QuickWheel）

**设计目标**：
- QuickWheel 作为可复用模块
- 未来可用于 VoiceWheel 等其他 Mod

## 尝试过的解决方案（❌ 失败）

### 1. 添加 [NonSerialized] 属性
**假设**：Unity 序列化系统导致类型加载失败

```csharp
[System.NonSerialized]
private ItemWheelAdapter _adapter;
```

**结果**：❌ 失败，错误依旧

---

### 2. 移除 readonly 修饰符
**假设**：readonly 字段与序列化冲突

```csharp
// 之前
private readonly Dictionary<...> _wheels = new ...();

// 修改后
private Dictionary<...> _wheels;
public ItemWheelSystem() { _wheels = new ...; }
```

**结果**：❌ 失败，错误依旧

---

### 3. 移除 Harmony 依赖
**假设**：QuickWheel.dll 依赖 Harmony，但找不到 Harmony 程序集

**操作**：从 QuickWheel.csproj 中注释掉 Harmony 引用

**结果**：❌ 失败，错误依旧（QuickWheel 本身不使用 Harmony）

---

### 4. 使用 AssemblyResolve 事件
**假设**：手动解析程序集可以解决加载问题

```csharp
static ModBehaviour()
{
    AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
}

private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
{
    var assemblyName = new AssemblyName(args.Name).Name;
    if (assemblyName == "QuickWheel" || assemblyName == "QuickWheel.UI")
    {
        var modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var dllPath = Path.Combine(modPath, assemblyName + ".dll");
        if (File.Exists(dllPath))
        {
            return Assembly.LoadFrom(dllPath);
        }
    }
    return null;
}
```

**结果**：❌ 失败，AssemblyResolve 事件从未触发

---

### 5. 使用 RuntimeInitializeOnLoadMethod
**假设**：在更早的生命周期预加载程序集

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void InitializeAssemblyResolver()
{
    AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
    PreloadDependencies();
}

private static void PreloadDependencies()
{
    var modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    Assembly.LoadFrom(Path.Combine(modPath, "QuickWheel.dll"));
    Assembly.LoadFrom(Path.Combine(modPath, "QuickWheel.UI.dll"));
}
```

**结果**：❌ 失败，类型加载发生在 Unity 初始化之前

---

## ✅ 最终解决方案：单 DLL 合并

### 核心问题

**Duckov 的 Mod 加载器不支持多 DLL 依赖解析**。

当 Mod 加载器尝试加载 ItemWheel.dll 时：
1. 发现类型 `CategoryWheel` 包含字段 `Wheel<Item>`
2. `Wheel<T>` 类型定义在 QuickWheel.dll 中
3. Mod 加载器**不会自动**在同目录搜索 QuickWheel.dll
4. 类型加载失败，抛出 TypeLoadException

这个问题发生在：
- ❌ **之前**：Unity 运行时初始化之前
- ❌ **之前**：任何 C# 代码执行之前
- ❌ **之前**：AssemblyResolve 事件注册之前

### 解决方案

**将 QuickWheel 源码编译到 ItemWheel.dll 中**，消除运行时依赖。

#### 修改 ItemWheel.csproj

```xml
<PropertyGroup>
  <TargetFramework>netstandard2.1</TargetFramework>
  <AssemblyName>ItemWheel</AssemblyName>
  <RootNamespace>ItemWheel</RootNamespace>
  <DuckovPath>D:\steam\steamapps\common\Escape from Duckov</DuckovPath>
  <OutputPath>$(DuckovPath)\Duckov_Data\Mods\ItemWheel</OutputPath>
</PropertyGroup>

<!-- 直接包含 QuickWheel 源码，编译到 ItemWheel.dll 中 -->
<ItemGroup>
  <Compile Include="..\QuickWheel\src\**\*.cs" />
</ItemGroup>
```

**关键变化**：
1. ❌ 移除 `<ProjectReference>` 引用
2. ❌ 移除 `<Reference>` DLL 引用
3. ✅ 添加 `<Compile Include="..\QuickWheel\src\**\*.cs" />` 直接包含源文件

#### 清理冲突文件

删除 ItemWheel 项目中的 `src/` 目录（与 QuickWheel 源码冲突）：
```bash
rm -rf D:\02_projects\Mod\Duckov\ItemWheel\src
```

#### 移除程序集加载代码

从 ModBehaviour.cs 中删除所有 AssemblyResolve 相关代码：
```csharp
// 删除以下代码：
// - static ModBehaviour()
// - OnAssemblyResolve()
// - PreloadDependencies()
// - InitializeAssemblyResolver()
```

#### 编译结果

**之前（多 DLL 方案）**：
- ItemWheel.dll: 16KB
- QuickWheel.dll: 30KB
- QuickWheel.UI.dll: 14KB
- **总计**：3个 DLL，运行时加载失败 ❌

**现在（单 DLL 方案）**：
- ItemWheel.dll: 48KB
- **总计**：1个 DLL，成功加载 ✅

## 为什么其他 Mod 不会遇到这个问题？

### Backpack_QuickWheel 对比

**Backpack_QuickWheel** 是原始的完整 Mod：
- 单一项目结构
- 所有代码在一个 DLL 中
- 没有外部依赖问题

**ItemWheel** 是重构后的模块化项目：
- QuickWheel 作为独立模块
- ItemWheel 依赖 QuickWheel
- ❌ 触发了 Duckov Mod 加载器的限制

## 核心教训

### 1. Duckov Mod 开发必须使用单 DLL

**❌ 不支持**：
- 多 DLL 依赖
- 运行时程序集解析
- ProjectReference 或 DLL Reference

**✅ 支持**：
- 单个 DLL 包含所有代码
- 通过 `<Compile Include="...">` 包含外部源文件
- 游戏 Managed 目录中的 DLL（Private=false）

### 2. 源码包含是最佳实践

对于需要复用的模块（如 QuickWheel）：
- **源码层面**：保持独立项目，便于维护和版本控制
- **编译层面**：通过 `<Compile Include="...">` 合并到主 DLL

### 3. Unity 类型加载时机非常早

类型加载发生在：
- ❌ 之前：任何 C# 代码执行
- ❌ 之前：静态构造函数
- ❌ 之前：RuntimeInitializeOnLoadMethod
- ❌ 之前：Awake/Start/Update

这意味着运行时程序集解析方案**完全无效**。

## 未来建议

### 对于新的 Mod 项目

1. **单 DLL 原则**：所有代码编译到一个 DLL
2. **源码包含**：复用代码通过 `<Compile Include="...">` 包含
3. **避免依赖**：不使用 ProjectReference 或外部 DLL Reference

### 对于 VoiceWheel 等未来项目

使用与 ItemWheel 相同的模式：

```xml
<!-- VoiceWheel.csproj -->
<ItemGroup>
  <Compile Include="..\QuickWheel\src\**\*.cs" />
</ItemGroup>
```

这样 QuickWheel 保持源码层面的复用，但编译成独立的 VoiceWheel.dll。

### 对于 QuickWheel 的发布

如果要发布 QuickWheel 给其他开发者使用：
- ❌ 不要发布 DLL（无法被其他 Mod 引用）
- ✅ 发布源码包或 NuGet 源码包
- ✅ 提供清晰的集成文档（如何通过 `<Compile Include>` 包含）

## 调试提示

### 识别程序集加载问题

当看到以下错误时，立即考虑单 DLL 方案：
```
TypeLoadException: Could not load file or assembly 'XXX, Version=...' or one of its dependencies.
```

### 验证编译结果

```bash
# 检查 Mod 目录中的 DLL 数量
ls D:\steam\steamapps\common\Escape from Duckov\Duckov_Data\Mods\YourMod\*.dll

# 应该只有一个主 DLL
# 如果有多个 DLL，说明有依赖问题
```

### 检查 DLL 大小

单 DLL 方案会让主 DLL 变大：
- 合并前：ItemWheel.dll (16KB) + QuickWheel.dll (30KB) = 46KB
- 合并后：ItemWheel.dll (48KB)

## 时间线

- **23:09-23:43**: 各种序列化相关尝试（NonSerialized, readonly, 构造函数）
- **23:43-23:57**: 移除 Harmony 依赖尝试
- **23:57-00:00**: AssemblyResolve 事件尝试
- **00:00-00:06**: RuntimeInitializeOnLoadMethod 尝试
- **00:06-00:07**: 单 DLL 合并方案 ✅ **成功**

**总耗时**：约1小时，尝试5种不同方案

## 相关文件

- `ItemWheel/ItemWheel.csproj` - 修改后的项目配置
- `ItemWheel/ModBehaviour.cs` - 清理后的 ModBehaviour（无程序集加载代码）
- `ItemWheel/ASSEMBLY_LOADING_ISSUE.md` - 本文档

## 参考

- Unity ScriptableObject 序列化：https://docs.unity3d.com/Manual/script-Serialization.html
- .NET Assembly.LoadFrom：https://learn.microsoft.com/en-us/dotnet/api/system.reflection.assembly.loadfrom
- MSBuild Compile Include：https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items

---

**记录日期**: 2025-11-06
**记录人**: Claude Code
**最终状态**: ✅ 问题已解决
