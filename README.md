# CardDisableControl

`CardDisableControl` 是《Slay the Spire 2》的卡牌禁用功能 Mod。
当前版本已实现卡牌总览禁用、详情页勾选禁用、红 X 覆盖显示与禁用配置持久化。

## 目标

- 提供可扩展的“卡牌禁用”能力。
- 在不污染底层权威数据的前提下，支持规则化禁用策略。
- 为后续补丁开发保留稳定的入口与日志链路。

## 功能说明（v1）

- 在卡牌总览界面，悬停卡牌按 `B` 可快速禁用/解禁。
- 在卡牌详情界面，“查看升级”旁新增“禁用卡牌”勾选项。
- 被禁用卡牌会在卡牌总览中覆盖红色 `X` 标记。
- 禁用会过滤长期随机出卡来源：
  - 奖励/事件随机卡池
  - 商店随机卡池
  - 非战斗转化随机卡池
- 战斗内临时生卡不受影响。

## 配置文件

- 路径：`%APPDATA%\SlayTheSpire2\mods\CardDisableControl\settings.json`
- 结构：
  - `schemaVersion: int`
  - `bannedCards: string[]`（canonical `ModelId` 字符串）
- 变更策略：每次禁用/解禁立即写盘（write-through）。

## baselib 使用状态

- 当前 **已使用 baselib 依赖**（`MegaCrit.Sts2.Core.*` 引用已配置）。
- 当前已用于初始化、UI补丁、输入处理与随机卡池过滤逻辑。

## 目录说明

```text
src/Mods/CardDisableControl/
├─ Scripts/
│  ├─ Entry.cs
│  └─ Patch/
├─ CardDisableControl.csproj
├─ mod_manifest.json
├─ README.md
└─ AGENTS.md
```

## 构建与检查

在 `src/Mods/CardDisableControl` 目录执行：

```powershell
dotnet restore CardDisableControl.csproj
dotnet build CardDisableControl.csproj -c Debug
dotnet format CardDisableControl.csproj --verify-no-changes
```

## 部署流程

1. 导出 pck：

```powershell
& "C:\Users\temp\项目\杀戮尖塔2Mod\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe" --path . --export-pack "Windows Desktop" CardDisableControl.pck
```

2. 复制到游戏目录：

```powershell
powershell -ExecutionPolicy Bypass -File .\copy_pck_to_game.ps1
```

3. 启动游戏：

```powershell
Start-Process "steam://rungameid/2868840"
```

说明：部署使用项目根产物 `CardDisableControl.dll` 与 `CardDisableControl.pck`。
