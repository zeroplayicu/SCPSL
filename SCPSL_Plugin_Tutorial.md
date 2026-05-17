# SCP秘密实验室插件开发完整教程

> 📅 更新时间：2026年5月15日  
> 🎮 适用版本：SCP: Secret Laboratory 11系列  
> 🔧 框架版本：EXILED 5.3.3

---

## 📋 目录

1. [环境要求](#环境要求)
2. [什么是EXILED](#什么是exiled)
3. [开发环境搭建](#开发环境搭建)
4. [插件基础结构](#插件基础结构)
5. [事件系统](#事件系统)
6. [配置系统](#配置系统)
7. [实战示例](#实战示例)
8. [编译与部署](#编译与部署)
9. [常见问题](#常见问题)

---

## 🖥️ 环境要求

### 必需软件

| 软件 | 版本要求 | 下载地址 |
|------|----------|----------|
| **.NET SDK** | 8.0 或更高 | [官方下载](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Visual Studio Code** | 最新版 | [官方下载](https://code.visualstudio.com/) |
| **C#扩展** | VS Code插件 | VS Code内搜索"ms-dotnettools.csharp" |

### 可选软件

| 软件 | 用途 |
|------|------|
| **Visual Studio 2022** | 更强大的IDE支持 |
| **Git** | 版本控制 |

---

## 🎯 什么是EXILED

**EXILED** 是 "EXtended In-runtime Library for External Development" 的缩写，是SCP秘密实验室的官方插件框架。

### 核心特点

- ✅ **基于Harmony** - 无需修改游戏程序集
- ✅ **事件驱动** - 通过事件系统操作游戏逻辑
- ✅ **热重载** - 支持插件动态重载
- ✅ **完全开源** - 代码透明可审计
- ✅ **跨版本兼容** - 游戏更新影响小

---

## 🔨 开发环境搭建

### 步骤1：安装.NET SDK

访问 [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0) 下载并安装：

**Windows用户**：下载 `dotnet-sdk-8.0.x-win-x64.exe`

**Linux用户**：
```bash
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0
```

### 步骤2：安装VS Code和C#扩展

1. 安装 [Visual Studio Code](https://code.visualstudio.com/)
2. 在VS Code中安装扩展：`Ctrl+Shift+X` 打开扩展商店
3. 搜索并安装 **C#** (ms-dotnettools.csharp)

### 步骤3：验证安装

打开终端/PowerShell，执行：

```bash
dotnet --version
```

应该显示 `8.0.x` 或更高版本。

---

## 📦 插件基础结构

### 项目结构

```
MyFirstPlugin/
├── MyFirstPlugin/
│   ├── MyFirstPlugin.csproj      # 项目文件
│   ├── Plugin.cs                  # 主插件类
│   ├── Config.cs                  # 配置文件
│   ├── EventHandlers.cs           # 事件处理类
│   └── obj/                       # 编译输出目录
├── MyFirstPlugin.sln              # 解决方案文件
└── README.md                      # 说明文档
```

### 最小化插件代码

#### Plugin.cs - 主插件类

```csharp
using System;
using Exiled.API.Features;
using Exiled.API.Interfaces;

namespace MyFirstPlugin
{
    public class Plugin : Plugin<Config>
    {
        // 插件元数据（公开发布必需）
        public override string Name => "MyFirstPlugin";
        public override string Author => "YourName";
        public override string Prefix => "myfirstplugin";
        public override Version Version => new Version(1, 0, 0);

        // 事件处理器实例
        public EventHandlers EventHandler { get; set; }

        // 插件启用时调用
        public override void OnEnabled()
        {
            Log.Info($"{Name} 插件已加载！");
            
            // 初始化事件处理器
            EventHandler = new EventHandlers(this);
            
            // 注册事件
            Exiled.Events.Handlers.Server.RoundStarted += EventHandler.OnRoundStarted;
            Exiled.Events.Handlers.Player.Verified += EventHandler.OnPlayerVerified;
            
            base.OnEnabled();
        }

        // 插件禁用时调用
        public override void OnDisabled()
        {
            Log.Info($"{Name} 插件已卸载！");
            
            // 注销事件
            Exiled.Events.Handlers.Server.RoundStarted -= EventHandler.OnRoundStarted;
            Exiled.Events.Handlers.Player.Verified -= EventHandler.OnPlayerVerified;
            
            EventHandler = null;
            
            base.OnDisabled();
        }
    }
}
```

#### Config.cs - 配置文件

```csharp
using System.ComponentModel;
using Exiled.API.Interfaces;

namespace MyFirstPlugin
{
    public class Config : IConfig
    {
        // IsEnabled 是必须的！EXILED用它来判断插件是否启用
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        [Description("玩家加入时显示的消息")]
        public string WelcomeMessage { get; set; } = "欢迎来到服务器！";

        [Description("欢迎消息显示时间（秒）")]
        public ushort WelcomeDuration { get; set; } = 5;

        [Description("是否启用调试模式")]
        public bool DebugMode { get; set; } = false;

        [Description("允许的权限组列表")]
        public string[] AllowedRoles { get; set; } = { "admin", "moderator" };
    }
}
```

#### EventHandlers.cs - 事件处理

```csharp
using System;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using Exiled.API.Features;

namespace MyFirstPlugin
{
    public class EventHandlers
    {
        // 保存插件实例的引用
        private readonly Plugin plugin;

        public EventHandlers(Plugin plugin)
        {
            this.plugin = plugin;
        }

        /// <summary>
        /// 玩家通过验证时触发
        /// </summary>
        public void OnPlayerVerified(VerifiedEventArgs ev)
        {
            // 获取配置值
            string message = plugin.Config.WelcomeMessage;
            ushort duration = plugin.Config.WelcomeDuration;

            // 向玩家发送广播消息
            ev.Player.Broadcast(duration, $"<color=yellow>{message}</color>");
            
            // 调试模式下输出日志
            if (plugin.Config.DebugMode)
            {
                Log.Debug($"玩家 {ev.Player.Nickname} 已加入服务器");
            }
        }

        /// <summary>
        /// 回合开始时触发
        /// </summary>
        public void OnRoundStarted()
        {
            Log.Info("新回合已开始！");
        }
    }
}
```

#### MyFirstPlugin.csproj - 项目文件

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>MyFirstPlugin</AssemblyName>
    <RootNamespace>MyFirstPlugin</RootNamespace>
    <Version>1.0.0</Version>
    <Authors>YourName</Authors>
    <Description>我的第一个SCP秘密实验室插件</Description>
  </PropertyGroup>

  <ItemGroup>
    <!-- EXILED NuGet包 -->
    <PackageReference Include="Exiled.API" Version="5.3.3" />
    <PackageReference Include="Exiled.Events" Version="5.3.3" />
    <PackageReference Include="Exiled.Loader" Version="5.3.3" />
  </ItemGroup>

</Project>
```

---

## 🎮 事件系统

### 事件分类

EXILED中的事件主要分为以下几类：

| 类别 | 命名空间 | 用途 |
|------|----------|------|
| **Server** | `Exiled.Events.Handlers.Server` | 服务器级别事件 |
| **Player** | `Exiled.Events.Handlers.Player` | 玩家相关事件 |
| **Map** | `Exiled.Events.Handlers.Map` | 地图/物体事件 |
| **Scp049** | `Exiled.Events.Handlers.Scp049` | SCP-049相关 |
| **Scp079** | `Exiled.Events.Handlers.Scp079` | SCP-079相关 |
| **Scp096** | `Exiled.Events.Handlers.Scp096` | SCP-096相关 |
| **Scp106** | `Exiled.Events.Handlers.Scp106` | SCP-106相关 |
| **Scp173** | `Exiled.Events.Handlers.Scp173` | SCP-173相关 |
| **Scp914** | `Exiled.Events.Handlers.Scp914` | SCP-914相关 |
| **Warhead** | `Exiled.Events.Handlers.Warhead` | 核弹事件 |
| **Vehicle** | `Exiled.Events.Handlers.Vehicle` | 载具事件 |

### 常用事件列表

#### Server事件

| 事件名 | 触发时机 | 事件参数 |
|--------|----------|----------|
| RoundStarted | 回合开始 | - |
| RoundEnded | 回合结束 | `RoundEndedEventArgs` |
| WaitingForPlayers | 等待玩家 | - |
| RespawningTeam | 生成队伍 | `RespawningTeamEventArgs` |
| PlayerLeft | 玩家离开 | `PlayerLeftEventArgs` |

#### Player事件

| 事件名 | 触发时机 | 事件参数 |
|--------|----------|----------|
| Verified | 玩家验证通过 | `VerifiedEventArgs` |
| Spawned | 玩家生成 | `SpawnedEventArgs` |
| Dying | 玩家死亡前 | `DyingEventArgs` |
| Died | 玩家死亡后 | `DiedEventArgs` |
| Hurt | 玩家受伤 | `HurtEventArgs` |
| InteractingElevator | 使用电梯 | `InteractingElevatorEventArgs` |
| InteractingDoor | 交互门 | `InteractingDoorEventArgs` |
| UsingItem | 使用物品 | `UsingItemEventArgs` |
| DroppingItem | 丢弃物品 | `DroppingItemEventArgs` |
| PickingUpItem | 捡起物品 | `PickingUpItemEventArgs` |

### 事件参数示例

```csharp
// DyingEventArgs 示例
public void OnPlayerDying(DyingEventArgs ev)
{
    // 获取攻击者
    Player attacker = ev.Attacker;
    
    // 获取受害者
    Player victim = ev.Target;
    
    // 获取伤害类型
    DamageTypes damageType = ev.DamageHandler.Type;
    
    // 取消事件（阻止死亡）
    // ev.IsAllowed = false;
}

// HurtEventArgs 示例  
public void OnPlayerHurt(HurtEventArgs ev)
{
    // 获取受伤玩家
    Player player = ev.Player;
    
    // 获取伤害值
    float damage = ev.Amount;
    
    // 获取伤害来源
    Player attacker = ev.Attacker;
    
    // 修改伤害值
    ev.Amount = damage * 0.5f; // 伤害减半
}
```

---

## ⚙️ 配置系统

### 配置类型

EXILED支持多种配置类型：

```csharp
public class Config : IConfig
{
    // 布尔类型
    public bool IsEnabled { get; set; } = true;
    
    // 字符串类型
    public string Message { get; set; } = "默认消息";
    
    // 整数类型
    public int MaxPlayers { get; set; } = 30;
    
    // 浮点数类型
    public float SpeedMultiplier { get; set; } = 1.5f;
    
    // 数组类型
    public string[] BannedItems { get; set; } = { "Gun", "Key" };
    
    // 列表类型
    public List<string> AdminCommands { get; set; } = new List<string>();
    
    // 字典类型
    public Dictionary<string, int> RoleCooldowns { get; set; }
        = new Dictionary<string, int>();
    
    // 枚举类型
    public BroadcastChannel Channel { get; set; } = BroadcastChannel.Normal;
}
```

### 配置特性

| 特性 | 用途 |
|------|------|
| `[Description("描述")]` | 设置配置项的说明文字 |
| `[Excluded]` | 排除配置（不保存到文件） |

### 动态配置读取

```csharp
public class Plugin : Plugin<Config>
{
    public override void OnEnabled()
    {
        // 直接访问配置
        Log.Info($"调试模式: {Config.DebugMode}");
        Log.Info($"欢迎消息: {Config.WelcomeMessage}");
        
        // 遍历数组配置
        foreach (var role in Config.AllowedRoles)
        {
            Log.Info($"允许的角色: {role}");
        }
    }
}
```

---

## 💡 实战示例

### 示例1：自定义欢迎消息插件

```csharp
// Plugin.cs
using Exiled.API.Features;

namespace WelcomePlugin
{
    public class Plugin : Plugin<Config>
    {
        public override string Name => "WelcomePlugin";
        public override string Author => "Developer";
        public override Version Version => new Version(1, 0, 0);

        public EventHandlers EventHandler { get; private set; }

        public override void OnEnabled()
        {
            EventHandler = new EventHandlers(this);
            Exiled.Events.Handlers.Player.Verified += EventHandler.OnPlayerVerified;
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Exiled.Events.Handlers.Player.Verified -= EventHandler.OnPlayerVerified;
            EventHandler = null;
            base.OnDisabled();
        }
    }
}

// Config.cs
using System.ComponentModel;
using Exiled.API.Interfaces;

namespace WelcomePlugin
{
    public class Config : IConfig
    {
        public bool IsEnabled { get; set; } = true;
        
        [Description("自定义欢迎消息")]
        public string WelcomeMessage { get; set; } = "欢迎来到服务器！";
        
        [Description("消息显示时长(秒)")]
        public ushort Duration { get; set; } = 5;
    }
}

// EventHandlers.cs
using Exiled.Events.EventArgs.Player;
using Exiled.API.Features;

namespace WelcomePlugin
{
    public class EventHandlers
    {
        private readonly Plugin plugin;

        public EventHandlers(Plugin plugin)
        {
            this.plugin = plugin;
        }

        public void OnPlayerVerified(VerifiedEventArgs ev)
        {
            ev.Player.Broadcast(
                plugin.Config.Duration,
                plugin.Config.WelcomeMessage
            );
        }
    }
}
```

### 示例2：踢出特定物品玩家

```csharp
// EventHandlers.cs
using Exiled.Events.EventArgs.Player;
using Exiled.API.Features;

namespace ItemBanPlugin
{
    public class EventHandlers
    {
        private readonly Plugin plugin;

        public EventHandlers(Plugin plugin)
        {
            this.plugin = plugin;
        }

        public void OnSpawned(SpawnedEventArgs ev)
        {
            // 检查玩家是否持有被禁止的物品
            foreach (var item in ev.Player.Items)
            {
                if (plugin.Config.BannedItems.Contains(item.Type.ToString()))
                {
                    // 踢出玩家并发送消息
                    ev.Player.Kick($"你携带了被禁止的物品: {item.Type}");
                    return;
                }
            }
        }
    }
}
```

### 示例3：修改玩家速度

```csharp
// EventHandlers.cs
using Exiled.Events.EventArgs.Player;
using Exiled.API.Features;
using UnityEngine;

namespace SpeedPlugin
{
    public class EventHandlers
    {
        private readonly Plugin plugin;

        public EventHandlers(Plugin plugin)
        {
            this.plugin = plugin;
        }

        public void OnSpawned(SpawnedEventArgs ev)
        {
            // 如果是MTF或CHI单位，应用速度修改
            if (ev.Player.Role.Side == Side.Mtf || ev.Player.Role.Side == Side.Chi)
            {
                // 使用反射设置玩家速度（示例）
                // 实际实现可能需要不同的方法
                Log.Info($"为玩家 {ev.Player.Nickname} 应用速度加成");
            }
        }
    }
}
```

---

## 🔧 编译与部署

### 编译插件

1. 打开终端，进入项目目录
2. 执行编译命令：

```bash
cd MyFirstPlugin
dotnet restore   # 恢复NuGet包
dotnet build     # 编译项目
```

3. 编译成功后，DLL文件会在 `bin/Debug/net8.0/` 或 `bin/Release/net8.0/` 目录

### 部署插件

1. 将编译生成的 **.dll** 文件复制到插件目录：

```
Windows: %AppData%\EXILED\Plugins\
Linux: ~/.config/EXILED/Plugins/
```

2. 重启服务器或使用 `reload` 命令重载插件

### 配置文件

插件首次运行后，会在配置目录生成配置文件：

```
Windows: %AppData%\EXILED\Configs\[端口号]-config.yml
Linux: ~/.config/EXILED/Configs/[端口号]-config.yml
```

---

## ❓ 常见问题

### Q1: 编译时提示 "找不到Exiled.API"

**原因**: 未正确添加NuGet包或未执行 `dotnet restore`

**解决方法**:
```bash
dotnet restore
```

### Q2: 插件不生效

**可能原因**:
1. 插件未放在正确目录
2. 配置文件中 `is_enabled` 为 false
3. 服务器路径包含中文字符

**解决方法**:
1. 确认DLL在 `EXILED/Plugins/` 目录
2. 检查配置文件
3. 避免使用中文路径

### Q3: 游戏更新后插件失效

**原因**: EXILED事件签名可能发生变化

**解决方法**:
1. 等待EXILED框架更新
2. 更新插件代码以适应新版本
3. 关注EXILED Discord获取最新信息

### Q4: 如何调试插件

**方法**:
1. 启用配置中的 `DebugMode`
2. 使用 `Log.Info()` 或 `Log.Debug()` 输出日志
3. 查看服务器日志文件

### Q5: 如何发布插件

**建议步骤**:
1. 确保代码完整且无错误
2. 添加完整的插件元数据（Name, Author, Version）
3. 在GitHub或Gitee创建仓库
4. 提交到EXILED插件列表（需审核）

---

## 📚 学习资源

### 官方资源

- **EXILED官网**: https://exiled-team.github.io/
- **EXILED GitHub**: https://github.com/Exiled-Team/EXILED
- **官方Discord**: 加入获取最新资讯和支持

### 推荐学习路径

1. 学习C#基础语法
2. 理解EXILED插件结构
3. 学习事件系统
4. 阅读优秀开源插件源码
5. 实践开发自己的插件

### 优秀开源插件参考

- **SCPSLAudioApi** - 音频API插件
- **Better047** - SCP-049改进插件
- **CustomItems** - 自定义物品插件

---

## 📝 笔记

> 本教程会持续更新。如有问题或建议，请在评论区留言！
>
> Happy coding! 🎮

---

**最后更新**: 2026年5月15日  
**适用框架**: EXILED 5.3.3
