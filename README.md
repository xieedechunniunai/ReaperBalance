# ReaperBalance - Reaper护符平衡性修改模组

[English Version](#english-version)

## 项目概述

ReaperBalance是一个针对Silksong游戏的Reaper护符平衡性修改模组，通过Harmony补丁和Unity组件系统动态修改游戏中的Reaper护符相关功能，提供可配置的平衡性调整选项。

## 功能特性

### 🎯 核心功能
- **全局开关控制** - 可随时启用/禁用所有平衡性修改
- **响应式配置更新** - 配置修改实时生效，无需重启游戏
- **护符装备检测** - 仅在装备Reaper护符时应用修改

### ⚔️ 战斗平衡调整
- **十字斩击修改**
  - 可配置的伤害倍率调整
  - 自定义攻击缩放大小
  - 基于钉子升级的动态伤害计算
- **普通攻击增强** - 可配置的普通攻击伤害倍率
- **下劈攻击强化** - 独立的下劈攻击伤害倍率

### 🔮 灵魂系统优化
- **灵魂吸收范围扩展** - 可配置的灵魂吸收检测范围
- **Reaper模式持续时间** - 延长Reaper模式的持续时间

### 🎮 用户体验
- **可视化配置界面** - 游戏内GUI配置面板
- **中英文支持** - 完整的本地化界面
- **实时日志输出** - 详细的调试信息

## 项目结构
Source/ 
├── Plugin.cs # 主插件入口，配置管理和Harmony补丁 
├── AssetManager.cs # 资源管理器，负责预制体加载 
├── Log.cs # 日志系统 
├── Behaviours/ 
│ ├── ChangeReaper.cs # 核心功能组件，处理所有Reaper修改 
│ └── ConfigGUI.cs # 配置界面实现 
└── Patches/ 
└── HeroControllerPatch.cs # Harmony补丁，修改HeroController行为

## 核心模块详解

### 1. Plugin.cs - 主插件模块
- **功能**: 插件入口点，配置初始化，Harmony补丁管理
- **特性**: 全局开关控制，场景切换监听，组件生命周期管理

### 2. ChangeReaper.cs - 核心功能组件
- **功能**: 实现所有Reaper护符的平衡性修改
- **子模块**:
  - 预制体缓存系统
  - 响应式伤害计算
  - 灵魂吸收范围修改
  - 攻击动作替换

### 3. ConfigGUI.cs - 配置界面
- **功能**: 游戏内可视化配置面板
- **特性**: 实时配置应用，中英文切换，用户友好界面

### 4. HeroControllerPatch.cs - Harmony补丁
- **功能**: 修改HeroController的Reaper模式行为
- **特性**: 条件性补丁应用，反射字段修改

### 5. AssetManager.cs - 资源管理
- **功能**: 游戏资源加载和缓存
- **特性**: 异步资源加载，预制体管理

## 安装说明

1. 确保已安装BepInEx框架
2. 将ReaperBalance.dll放入`BepInEx/plugins`目录
3. 启动游戏，模组将自动加载

## 配置选项

在游戏内通过 Mod Options 菜单配置，或编辑`BepInEx/config`目录下的配置文件：

- `EnableReaperBalance`: 全局启用/禁用开关 (默认: true)
- `UseChinese`: 是否使用中文菜单 (默认: true)
- `EnableCrossSlash`: 是否启用十字斩 (默认: true)
- `EnableSilkAttraction`: 是否吸引小丝球 (默认: true)
- `CrossSlashScale`: 十字斩击缩放大小 (默认: 1.2)
- `CrossSlashDamage`: 十字斩伤害倍率 (默认: 2.3)
- `NormalAttackMultiplier`: 普通攻击倍率 (默认: 1.2)
- `DownSlashMultiplier`: 下劈攻击倍率 (默认: 1.5)
- `CollectRange`: 吸引范围 (默认: 8)
- `CollectMaxSpeed`: 吸引最大速度 (默认: 20)
- `CollectAcceleration`: 吸引加速度 (默认: 800)
- `DurationMultiplier`: 持续时间倍率 (默认: 3.0)

## 技术特点

- **模块化设计**: 各功能模块独立，便于维护和扩展
- **错误处理**: 完善的异常处理和日志记录
- **性能优化**: 预制体缓存，避免重复资源加载
- **兼容性**: 遵循Harmony补丁最佳实践

## 开发环境

- Unity Engine
- .NET Standard 2.1
- BepInEx 5.x
- HarmonyX

## 许可证

本项目采用MIT许可证，详见LICENSE.md文件。

---

# English Version

## Project Overview

ReaperBalance is a balance modification mod for the Reaper charm in Silksong game. It dynamically modifies Reaper charm related functionalities through Harmony patches and Unity component system, providing configurable balance adjustment options.

## Features

### 🎯 Core Features
- **Global Toggle Control** - Enable/disable all balance modifications at any time
- **Responsive Configuration Updates** - Configuration changes take effect in real-time without restarting the game
- **Charm Equipment Detection** - Modifications only apply when Reaper charm is equipped

### ⚔️ Combat Balance Adjustments
- **Cross Slash Modifications**
  - Configurable damage multiplier adjustments
  - Custom attack scaling size
  - Dynamic damage calculation based on nail upgrades
- **Normal Attack Enhancement** - Configurable normal attack damage multiplier
- **Down Slash Reinforcement** - Independent down slash damage multiplier

### 🔮 Soul System Optimization
- **Soul Absorption Range Extension** - Configurable soul absorption detection range
- **Reaper Mode Duration** - Extended Reaper mode duration

### 🎮 User Experience
- **Visual Configuration Interface** - In-game GUI configuration panel
- **Chinese/English Support** - Complete localization interface
- **Real-time Log Output** - Detailed debugging information

## Project Structure

Source/ 
├── Plugin.cs # Main plugin entry, configuration management and Harmony patches 
├── AssetManager.cs # Resource manager, responsible for prefab loading 
├── Log.cs # Logging system ├── Behaviours/ 
│ ├── ChangeReaper.cs # Core functionality component, handles all Reaper modifications 
│ └── ConfigGUI.cs # Configuration interface implementation 
└── Patches/ 
└── HeroControllerPatch.cs # Harmony patch, modifies HeroController behavior

## Core Modules Detailed

### 1. Plugin.cs - Main Plugin Module
- **Function**: Plugin entry point, configuration initialization, Harmony patch management
- **Features**: Global toggle control, scene change listening, component lifecycle management

### 2. ChangeReaper.cs - Core Functionality Component
- **Function**: Implements all Reaper charm balance modifications
- **Sub-modules**:
  - Prefab caching system
  - Responsive damage calculation
  - Soul absorption range modification
  - Attack action replacement

### 3. ConfigGUI.cs - Configuration Interface
- **Function**: In-game visual configuration panel
- **Features**: Real-time configuration application, Chinese/English switching, user-friendly interface

### 4. HeroControllerPatch.cs - Harmony Patch
- **Function**: Modifies HeroController's Reaper mode behavior
- **Features**: Conditional patch application, reflection field modification

### 5. AssetManager.cs - Resource Management
- **Function**: Game resource loading and caching
- **Features**: Asynchronous resource loading, prefab management

## Installation Instructions

1. Ensure BepInEx framework is installed
2. Place ReaperBalance.dll into `BepInEx/plugins` directory
3. Launch the game, the mod will load automatically

## Configuration Options

Configure through the in-game Mod Options menu, or edit configuration files in `BepInEx/config` directory:

- `EnableReaperBalance`: Global enable/disable toggle (Default: true)
- `UseChinese`: Use Chinese menu language (Default: true)
- `EnableCrossSlash`: Enable Cross Slash (Default: true)
- `EnableSilkAttraction`: Enable silk orb attraction (Default: true)
- `CrossSlashScale`: Cross slash scaling size (Default: 1.2)
- `CrossSlashDamage`: Cross slash damage multiplier (Default: 2.3)
- `NormalAttackMultiplier`: Normal attack multiplier (Default: 1.2)
- `DownSlashMultiplier`: Down slash multiplier (Default: 1.5)
- `CollectRange`: Attraction range (Default: 8)
- `CollectMaxSpeed`: Max attraction speed (Default: 20)
- `CollectAcceleration`: Attraction acceleration (Default: 800)
- `DurationMultiplier`: Duration multiplier (Default: 3.0)

## Technical Features

- **Modular Design**: Independent functional modules for easy maintenance and extension
- **Error Handling**: Comprehensive exception handling and logging
- **Performance Optimization**: Prefab caching to avoid repeated resource loading
- **Compatibility**: Follows Harmony patch best practices

## Development Environment

- Unity Engine
- .NET Standard 2.1
- BepInEx 5.x
- HarmonyX

## License

This project uses MIT License, see LICENSE.md file for details.

