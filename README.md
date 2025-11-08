# ItemWheel - 物品/近战/子弹 轮盘 Mod

ItemWheel 是基于 QuickWheel 的轮盘交互 Mod，提供“物品轮盘”“近战轮盘（V）”“子弹轮盘（R）”三大功能，支持拖拽重排、两行右对齐信息（数量/耐久 + 名称 上色）。

## 功能一览

- 物品轮盘：自动收集背包常用物品，悬停/点击即可使用
- 近战轮盘（V）：列出近战武器，悬停/点击直接切到手上
- 子弹轮盘（R）：
  - 短按 R：原生换弹不变
  - 长按 R：呼出轮盘，显示当前武器可用弹种，悬停/点击即切弹
- 拖拽重排：同一轮盘内拖动两个格子交换位置（空格不接收，红色无效提示）
- 关闭防误触：本次打开期间若调过布局，关闭时不误用物品
- 两行右对齐：
  - 上行：数量（xN）或耐久百分比（%）
  - 下行：名称（按稀有度着色；子弹按原始品质颜色）

## 与 QuickWheel 的关系

- ItemWheel 通过 `ItemWheel.csproj` 直接编译引用 `../QuickWheel/src/**/*.cs` 源码（嵌入式依赖）
- UI/交互由 QuickWheel 提供（Wheel/WheelBuilder/WheelUIManager 等）；ItemWheel 仅负责业务收集/映射/确认
- 轻量补丁：仅在 `SetData` 后置将 Label 调整为右下对齐，启用富文本（不改 QuickWheel 结构）

## 代码结构（节选）

- `ItemWheelSystem.cs` 物品轮盘主流程（收集/映射/显示/确认/拖拽回调）
- `AmmoWheelSystem.cs` 子弹轮盘（短按换弹、长按轮盘切弹）
- `ItemWheelAdapter.cs` / `BulletWheelAdapter.cs` 适配器：游戏 `Item` → `IWheelItem`
- `WheelItemWithDecor.cs` 名称上色、数量/耐久文本（富文本）
- `LabelAlignPatch.cs` 小补丁：Label 右下对齐 + 允许两行
- `RarityColorProvider.cs` 颜色映射（普通物品“再降一级”，子弹按原品质色）

## 使用说明（默认键位）

- 物品轮盘：按住各类快捷键（与原游戏一致）长按呼出，松手确认
- 近战轮盘：`V` 长按呼出，松手确认；悬停/点击均可切到手上
- 子弹轮盘：
  - `R` 短按：原生换弹
  - `R` 长按：呼出轮盘 → 悬停/点击切换弹种

## 安装与构建

- 构建：`dotnet build -c Release`
- 输出：`Duckov_Data/Mods/ItemWheel/ItemWheel.dll`
- 资源：确保以下纹理存在（格子背景）：
  - `texture/WheelSlot_Normal.png`
  - `texture/WheelSlot_Hover.png`
  - `texture/WheelSlot_Selected.png`

## 配置与手感

- 死区半径：40（像素，已内置，手感更稳）
- 两行右对齐：无需 UI 预制，纯文本实现；如需关闭富文本可在代码中调整 `WheelItemWithDecor`

## 兼容性与注意

- 语音轮盘（F1）后续将复用 QuickWheel，仅影响文本层，不破坏交互
- 不建议直接改 QuickWheel 的核心 UI/交互类（WheelSlotDisplay/WheelUIManager），ItemWheel 已通过最小补丁方式对齐需求

---

配套版本：ItemWheel v1.1.0 / QuickWheel v1.1.0

