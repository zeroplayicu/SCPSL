# 🎮 SCP:SL 服务器插件合集

这是一套为 **SCP: Secret Laboratory** 服务器打造的 **LabAPI 插件**合集。  
包含经验等级、聊天系统、物品清理、指挥官护盾等功能。

> 💡 **如果你是第一次架设服务器，别担心！下面的教程一步一步带你走。**

---

## 📦 这个合集里有什么？

| 图标 | 插件 | 一句话说明 |
|:----:|------|-----------|
| ⭐ | **ExperiencePlugin** | 打怪升级！击杀/伤害得经验，自动升级加 Buff |
| 💬 | **ChatPlugin** | 全体聊天、同阵营聊天、查看效果 |
| 🧹 | **CleanupPlugin** | 自动清理地上太多掉落物，防止服务器卡顿 |
| 🛡️ | **CommanderShieldPlugin** | NTF队长自动获得护盾，能抗伤害还能看剩余盾量 |

### 🆕 EXILED 修复版插件（2026-09 更新）

上面四个是 **LabAPI 版**（标签【labapi】）。下面是 **EXILED 版**（标签【ex】），源码在 [`EXILED修复版/`](./EXILED修复版/) 目录，修复了大量 BUG 和 UI 问题：

| 图标 | 插件 | 一句话说明 |
|:----:|------|-----------|
| 🛠️ | **AdminTools** | 管理员等级管理工具 |
| 🔪 | **AntiTeamKillPlugin** | 防队友误伤（TK 处理与豁免） |
| 📖 | **AutoTutorialPlugin** | 进服自动变教程角色；准备倒计时不足 3 秒变观察者 |
| 💬 | **ChatPlugin** | 全体/团队聊天 + 效果与生涯查询（EXILED 版） |
| 🧹 | **CleanupPlugin** | 掉落物自动清理（EXILED 版） |
| ⭐ | **ExperiencePlugin** | 经验等级系统 EXILED 版，含兑换码（CDK）与抽奖功能 |
| 🐙 | **Scp999** | SCP-999 挠痒怪三技能改造版：主技能（治疗）/ 技能2（Yippee）/ 技能3（搞笑动画），按键不预设，玩家在 设置→服务器专属设置 中自行绑定 |

> 💡 编译产物（中文命名的 DLL，带【ex】/【labapi】标签）在 **Releases** 页面下载。

---

## 🔧 第一步：怎么安装？（超简单）

### 如果你有服务器控制权：

1. 打开服务器的这个文件夹（复制到地址栏回车）：
   ```
   %AppData%\SCP Secret Laboratory\LabAPI\plugins\
   ```
2. 把 Releases 里面的 **4个 `.dll` 文件** 复制进去
3. **重启服务器**
4. 搞定！🎉

> 插件会自动生成配置文件，路径在：
> `%AppData%\SCP Secret Laboratory\LabAPI\configs\` 下

---

## 🎮 各插件用法（小白版）

---

### ⭐ ExperiencePlugin — 经验等级系统

#### 怎么玩？

- **打人得经验**：每造成 1 点伤害 = 1 经验，击杀一个人 = 100 经验
- **自动升级**：经验够了自动升，越高需要的经验越多
- **等级奖励**：
  - 25 级 → 移速加快 🏃
  - 50 级 → 移速更快 🚀
  - 100 级 → 移速飞快 💨
- **无限子弹**：换弹自动补满，不用担心没子弹
- **KDA 显示**：屏幕底部一直显示你的 `击杀/死亡/助攻`
- **D 级人员**开局自动发一张清洁工卡

#### 屏幕上显示什么？

```
━━ 玩家名 | Lv.5 60/500 | 3/2/1 | 30分钟 ━━
```
- `3/2/1` = 本局击杀/死亡/助攻

---

### 💬 ChatPlugin — 聊天系统

在游戏里聊天框输入：

| 命令 | 别名 | 效果 |
|------|:----:|------|
| `.bc 大家好` | `.broadcast` / `.all` | **全体玩家**都能看到 |
| `.c 有人吗` | `.team` / `.t` | 只有**同阵营**的人能看到 |
| `.buff` | `.effects` / `.e` | 查看自己身上有什么效果 |
| `.info` | `.career` / `.stats` | 查看自己的生涯数据 |

> 💡 **注意**：命令前面的 `.` 要打，比如 `.bc 你好`

---

### 🧹 CleanupPlugin — 掉落物清理

#### 这东西干嘛的？

服务器打久了地上全是枪和子弹，会**卡顿**。这个插件会自动清理。

- 每 **10 秒** 检查一次
- 地上超过 **250 个** 掉落物 → 全屏提示 ⚠️
- **5 秒倒计时**后自动全部清空
- **SCP 物品**（500/207/268 等）不会被清理，放心

---

### 🛡️ CommanderShieldPlugin — NTF 指挥官护盾

#### 谁可以用？

只有 **NTF 队长（九尾狐队长）** 才能激活。

#### 效果：

- 获得 **50 AHP**（人工生命值，右下角蓝条）
- 获得 **100 HS**（隐藏护盾值）
- 指挥官卡自动换成 **O5 权限卡**
- **每秒自动恢复** 1 点
- 屏幕右侧显示护盾数值和进度条：
  ```
  ═══ 量子护盾 (NTF指挥官) ═══
  AHP ████████░░  40/50
  HS  ██████████  100/100
  ```
- **绿色** = 护盾充足，**橙色** = 快没了，**红色** = 危险！

> 先消耗 AHP，AHP 用完了再消耗 HS

---

## ⚙️ 进阶：怎么改配置？

配置文件在：
```
%AppData%\SCP Secret Laboratory\LabAPI\configs\
```

每个插件有自己单独的文件夹，里面有个 `config.yml` 文件，用**记事本**就能打开修改。

### ExperiencePlugin 常用配置：

```yaml
ExpPerKill: 100           # 杀一个人给多少经验
ExpPerDamage: 1           # 打1点血给多少经验
BaseExpPerLevel: 100      # 每级需要的基础经验
EnableInfiniteAmmo: true  # 无限子弹开关
Scp207NoDrain: true       # SCP207是否掉血
ShowStatusAlways: true    # 是否一直显示底部状态栏
```

### CleanupPlugin 常用配置：

```yaml
CleanupThreshold: 250     # 超过多少个开始清理
CheckInterval: 10         # 多久检查一次（秒）
CleanRagdolls: true       # 是否连尸体一起清理
```

### CommanderShieldPlugin 常用配置：

```yaml
MaxShieldAHP: 50          # AHP最大值
MaxShieldHS: 100          # HS最大值
RegenPerTick: 1           # 每秒回复量
ReplaceCommanderCard: true # 是否发O5卡
```

---

## 🛠️ 开发：如何自己编译？

需要安装 [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

**LabAPI 版**（仓库根目录的各插件文件夹）：

```bash
dotnet build ExperiencePlugin -c Release
dotnet build CommanderShieldPlugin -c Release
```

**EXILED 修复版**（`EXILED修复版/` 目录）：

1. 从 **Releases** 下载 `插件编译依赖包.zip`，解压到 `EXILED修复版/Dependencies/`
2. EXILED 走 NuGet 自动还原；游戏程序集（Assembly-CSharp 等）通过 `SCPSLManagedPath` 参数指定本机服务器 `SCPSL_Data/Managed` 目录：

```bash
dotnet build "EXILED修复版/Scp999" -c Release \
  -p:SCPSLManagedPath="D:\SCPSL\SCPSL_Data\Managed"
```

编译好的 DLL 在 `项目文件夹\bin\Release\net48\` 下。

---

## 📁 文件结构

```
SCPSL/
├── ExperiencePlugin/          ← LabAPI 版经验插件（【labapi】）
├── CommanderShieldPlugin/     ← LabAPI 版指挥官护盾（【labapi】）
├── ChatPlugin/                ← LabAPI 版聊天插件（【labapi】）
├── CleanupPlugin/             ← LabAPI 版清理插件（【labapi】）
├── MyFirstPlugin/             ← 示例插件
├── EXILED修复版/              ← EXILED 插件（【ex】，2026-09 修复版）
│   ├── AdminTools/
│   ├── AntiTeamKillPlugin/
│   ├── AutoTutorialPlugin/
│   ├── ChatPlugin/
│   ├── CleanupPlugin/
│   ├── ExperiencePlugin/      ← EXILED 版（含兑换码、抽奖）
│   ├── Scp999/                ← SCP-999 三技能改造版
│   └── Directory.Build.props  ← 游戏程序集引用配置
└── .github/workflows/
```

---

## 📋 技术信息

| 项目 | 版本 |
|------|------|
| 框架 | LabAPI 1.1.6.1 |
| 运行环境 | .NET Framework 4.8 |
| 语言 | C# 12.0 |
| 数据存储 | YAML (YamlDotNet) |

---

> 🌟 **有问题？** 在 GitHub 提 Issue 或找服务器管理员咨询！
