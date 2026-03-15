﻿# AGENTS.md - CardDisableControl Mod 协作规范

本文件面向自动化编码代理，目标是让代理在本项目中稳定改动、可验证、可回滚。

## 0. 作用域与硬性约束

- 仅允许修改 `src/Mods/CardDisableControl/`。
- `src/Core/`、`src/gdscript/`、`src/GameInfo/` 视为只读参考源码。
- 不要删除用户已有改动，不要用 `git reset --hard`、`git checkout --`。
- 语言：与用户沟通、日志说明、注释均用中文；标识符保持英文。
- 提交策略：每次会话完成代码修改后，立即提交一次 commit（除非用户明确要求不提交）。

## 1. 项目背景（必须理解）

- 项目名称：`CardDisableControl`。
- 目标方向：实现卡牌禁用功能，支持规则化禁用与可回收控制。
- 当前阶段：仅有工程骨架，已引用 baselib 依赖。
- 设计原则：先保证状态安全与可回滚，再逐步扩展玩法逻辑。

## 2. 相关知识

知识库位于 `../../知识库/Mod 开发指南/`，包含从 SlayTheSpire2ModdingTutorials 克隆的 Mod 开发教程。

若使用了 baselib，必须在 README.md 中说明当前是否使用。

## 3. 代码位置

- 入口：`Scripts/Entry.cs`
- 补丁目录：`Scripts/Patch/`
- 当前占位补丁：`Scripts/Patch/CardDisableControlPatch.cs`

## 4. Build / Lint / Test 命令

在目录 `src/Mods/CardDisableControl` 下执行。

- 依赖恢复：`dotnet restore CardDisableControl.csproj`
- Debug 构建：`dotnet build CardDisableControl.csproj -c Debug`
- Release 构建：`dotnet build CardDisableControl.csproj -c Release`
- 检查格式：`dotnet format CardDisableControl.csproj --verify-no-changes`
- 自动格式化：`dotnet format CardDisableControl.csproj`

## 5. 构建产物路径约束

- `Godot.NET.Sdk` 默认会先把编译产物输出到 `.godot/mono/temp/bin/<Config>/CardDisableControl.dll`。
- 项目通过 `CopyDllToProjectRoot` 目标会再复制一份到项目根：`CardDisableControl.dll`。
- 部署、联调、对外引用统一使用项目根 `CardDisableControl.dll`。

## 6. 会话结束前默认部署动作

当本次会话包含代码修改，且 Debug 构建成功并产出项目根 DLL 后，默认执行：

1. `& "C:\Users\temp\项目\杀戮尖塔2Mod\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe" --path . --export-pack "Windows Desktop" CardDisableControl.pck`
2. `powershell -ExecutionPolicy Bypass -File .\copy_pck_to_game.ps1`
3. `Start-Process "steam://rungameid/2868840"`

若用户明确说明不执行部署脚本或不自动启动游戏，则按用户要求跳过。
