# ReaperBalance - 收割者纹章增强模组

[English Version](#english-version)

## 模组简介

ReaperBalance 是一款针对 Silksong 收割者纹章（Reaper Crest）的增强模组。装备收割者纹章后，你将获得更强大的战斗能力和更流畅的游戏体验。所有功能都可以通过游戏内菜单自由配置。

## 主要功能

### 战斗增强
- **十字斩蓄力攻击** - 蓄力后释放强力的十字形斩击，可调整大小和伤害
- **攻击伤害提升** - 普通攻击和下劈攻击的伤害倍率可独立调整
- **眩晕值加成** - 所有攻击的眩晕效果更强，更容易打断敌人

### 收割模式优化
- **持续时间延长** - 收割者模式持续时间大幅提升
- **丝球掉落增加** - 攻击敌人时掉落更多丝球
- **丝球自动吸引** - 远距离自动吸引小丝球，可调整范围和速度

### 暴击系统（可选）
- **独享暴击机制** - 启用后，收割者纹章拥有独立的暴击系统
- **可配置暴击率** - 自定义暴击概率（0-100%）
- **可配置暴击伤害** - 自定义暴击伤害倍率

## 安装方法

### 前置依赖
- [BepInEx](https://github.com/BepInEx/BepInEx) - 模组加载框架
- [ModMenu](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/) - 游戏内配置菜单（[GitHub](https://github.com/silksong-modding/Silksong.ModMenu)）

### 安装步骤
1. 安装 BepInEx 框架
2. 安装 ModMenu 模组
3. 将 `ReaperBalance.dll` 放入 `BepInEx/plugins` 目录
4. 启动游戏即可

## 配置说明

在游戏内通过 **Mod Options** 菜单配置（支持中英文切换），或编辑 `BepInEx/config/ReaperBalance.cfg` 文件。

### 通用设置
| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| EnableReaperBalance | 全局启用/禁用开关 | true |
| UseChinese | 使用中文菜单 | true |

### 攻击设置
| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| EnableCrossSlash | 启用十字斩蓄力攻击 | true |
| CrossSlashScale | 十字斩缩放大小 | 1.2 |
| CrossSlashDamage | 十字斩伤害倍率 | 2.3 |
| NormalAttackMultiplier | 普通攻击伤害倍率 | 1.2 |
| DownSlashMultiplier | 下劈攻击伤害倍率 | 1.5 |
| StunDamageMultiplier | 眩晕值倍率 | 1.2 |

### 收割模式设置
| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| DurationMultiplier | 收割者模式持续时间倍率 | 3.0 |
| ReaperBundleMultiplier | 丝球掉落倍率 | 1.0 |

### 丝球吸引设置
| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| EnableSilkAttraction | 启用丝球吸引 | true |
| CollectRange | 吸引范围 | 8 |
| CollectMaxSpeed | 吸引最大速度 | 20 |
| CollectAcceleration | 吸引加速度 | 800 |

### 暴击设置
| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| EnableReaperCrit | 启用收割者暴击系统 | false |
| ReaperCritChancePercent | 暴击率（%） | 10 |
| ReaperCritDamageMultiplier | 暴击伤害倍率 | 3.0 |

---

# 开发者文档

## 项目结构

```
Source/
├── Plugin.cs                    # 主插件入口，配置管理
├── AssetManager.cs              # 资源管理器，预制体加载缓存
├── Log.cs                       # 日志系统
├── Behaviours/
│   └── ChangeReaper.cs          # 核心功能组件
├── ModMenu/
│   └── ReaperBalancePaginatedMenuScreen.cs  # 分页菜单界面
└── Patches/
    ├── HeroControllerPatch.cs   # HeroController 补丁
    └── HealthManagerCritPatch.cs # 暴击系统补丁
```

## 核心模块

### Plugin.cs
- 插件入口点，BepInEx 配置初始化
- Harmony 补丁注册
- ModMenu 界面构建
- 场景切换监听和组件生命周期管理

### ChangeReaper.cs
- 实现十字斩蓄力攻击
- 伤害倍率计算
- 丝球吸引逻辑
- 攻击动作替换

### HeroControllerPatch.cs
- 修改 `BindCompleted` 方法延长收割者模式持续时间
- 修改 `GetReaperPayout` 方法调整丝球掉落倍率

### HealthManagerCritPatch.cs
- 拦截 `ApplyDamageScaling` 方法
- 实现独立的暴击判定和伤害计算

## 技术特点

- **模块化设计** - 各功能模块独立，便于维护扩展
- **响应式配置** - 配置修改实时生效，无需重启
- **条件性补丁** - 仅在装备收割者纹章时应用修改
- **性能优化** - 预制体缓存，避免重复资源加载

## 开发环境

- Unity 6000.0.50
- .NET Standard 2.1
- BepInEx 5.x
- HarmonyX
- Silksong.ModMenu

## 许可证

MIT License

---

# English Version

## Introduction

ReaperBalance is an enhancement mod for Silksong's Reaper Crest. When equipped, you'll gain more powerful combat abilities and a smoother gameplay experience. All features can be freely configured through the in-game menu.

## Main Features

### Combat Enhancement
- **Cross Slash Charge Attack** - Release a powerful cross-shaped slash after charging
- **Attack Damage Boost** - Independent damage multipliers for normal and down slash attacks
- **Stun Bonus** - Stronger stun effects on all attacks

### Reaper Mode Optimization
- **Extended Duration** - Significantly longer Reaper mode duration
- **Increased Silk Drops** - More silk orbs drop when attacking enemies
- **Auto Silk Attraction** - Automatically attract silk orbs from a distance

### Critical System (Optional)
- **Exclusive Crit Mechanic** - Independent critical hit system for Reaper Crest
- **Configurable Crit Rate** - Custom crit chance (0-100%)
- **Configurable Crit Damage** - Custom crit damage multiplier

## Installation

### Dependencies
- [BepInEx](https://github.com/BepInEx/BepInEx) - Mod loading framework
- [ModMenu](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/) - In-game configuration menu ([GitHub](https://github.com/silksong-modding/Silksong.ModMenu))

### Steps
1. Install BepInEx framework
2. Install ModMenu mod
3. Place `ReaperBalance.dll` into `BepInEx/plugins` directory
4. Launch the game

## Configuration

Configure through the in-game **Mod Options** menu (supports Chinese/English), or edit `BepInEx/config/ReaperBalance.cfg`.

### General Settings
| Option | Description | Default |
|--------|-------------|---------|
| EnableReaperBalance | Global enable/disable toggle | true |
| UseChinese | Use Chinese menu | true |

### Attack Settings
| Option | Description | Default |
|--------|-------------|---------|
| EnableCrossSlash | Enable cross slash charge attack | true |
| CrossSlashScale | Cross slash scale | 1.2 |
| CrossSlashDamage | Cross slash damage multiplier | 2.3 |
| NormalAttackMultiplier | Normal attack damage multiplier | 1.2 |
| DownSlashMultiplier | Down slash damage multiplier | 1.5 |
| StunDamageMultiplier | Stun damage multiplier | 1.2 |

### Reaper Mode Settings
| Option | Description | Default |
|--------|-------------|---------|
| DurationMultiplier | Reaper mode duration multiplier | 3.0 |
| ReaperBundleMultiplier | Silk orb drop multiplier | 1.0 |

### Silk Attraction Settings
| Option | Description | Default |
|--------|-------------|---------|
| EnableSilkAttraction | Enable silk orb attraction | true |
| CollectRange | Attraction range | 8 |
| CollectMaxSpeed | Attraction max speed | 20 |
| CollectAcceleration | Attraction acceleration | 800 |

### Critical Settings
| Option | Description | Default |
|--------|-------------|---------|
| EnableReaperCrit | Enable Reaper crit system | false |
| ReaperCritChancePercent | Crit chance (%) | 10 |
| ReaperCritDamageMultiplier | Crit damage multiplier | 3.0 |

## License

MIT License
