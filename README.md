# 🎮 SCP:SL 服务器插件合集

这是一套为 **SCP: Secret Laboratory** 服务器打造的 **EXILED 插件**合集。  
包含经验等级、聊天系统、物品清理、指挥官护盾、反组杀等功能。

> 💡 **如果你是第一次架设服务器，别担心！下面的教程一步一步带你走。**

---

## 📦 这个合集里有什么？

| 图标 | 插件 | 一句话说明 |
|:----:|------|-----------|
| ⭐ | **ExperiencePlugin** | 打架升级！击杀/伤害得经验，自动升级加 Buff |
| 💬 | **ChatPlugin** | 全体聊天、同阵营聊天、查看效果、生涯数据 |
| 🧹 | **CleanupPlugin** | 自动清理地上太多掉落物，防止服务器卡顿 |
| 🛡️ | **CommanderShieldPlugin** | NTF队长自动获得护盾，能抗伤害还能看剩余盾量 |
| 🚫 | **AntiTeamKillPlugin** | 反恶意组杀 + 警告系统 + 玩家管理沟通 |

---

## 🔧 第一步：怎么安装？（超简单）

### 如果你有服务器控制权：

1. 打开服务器的这个文件夹（复制到地址栏回车）：
   ```
   %AppData%\EXILED\Plugins\
   ```
2. 把编译好的 **`.dll` 文件** 复制进去（每个插件一个 .dll）
3. **重启服务器**
4. 搞定！🎉

> 插件会自动生成配置文件，路径在：
> `%AppData%\EXILED\Configs\` 下

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
- 攻击SCP/人类可回复护盾值
- 屏幕右侧显示护盾数值和进度条：
  ```
  ═══ 量子护盾 ═══
  AHP ████████░░  40/50
  HS  ██████████  100/100
  ```
- **绿色** = 护盾充足，**橙色** = 快没了，**红色** = 危险！

> 先消耗 AHP，AHP 用完了再消耗 HS

---

### 🚫 AntiTeamKillPlugin — 反组杀 + 警告系统

#### 功能：

- 攻击队友自动扣HP + 扣经验
- 击杀队友扣大量经验
- 本局击杀队友超过阈值 → 自动变为教程角色
- `.AC <消息>` 向管理员发送消息
- `.ma` 被恶意组杀后变为教程角色查看教程
- `.warr` 查看警告记录 / 管理员管理警告

---

## ⚙️ 进阶：怎么改配置？

配置文件在：
```
%AppData%\EXILED\Configs\
```

每个插件有自己单独的配置文件（YAML格式），用**记事本**就能打开修改。

### ExperiencePlugin 常用配置：

```yaml
exp:
  exp_per_kill: 100           # 杀一个人给多少经验
  exp_per_damage: 1           # 打1点血给多少经验
  base_exp_per_level: 100     # 每级需要的基础经验
  enable_infinite_ammo: true  # 无限子弹开关
  scp207_no_drain: true       # SCP207是否掉血
  show_status_always: true    # 是否一直显示底部状态栏
```

### CleanupPlugin 常用配置：

```yaml
cleanup:
  cleanup_threshold: 250      # 超过多少个开始清理
  check_interval: 10          # 多久检查一次（秒）
  clean_ragdolls: true        # 是否连尸体一起清理
```

### CommanderShieldPlugin 常用配置：

```yaml
cshield:
  max_shield_ahp: 50          # AHP最大值
  max_shield_hs: 100          # HS最大值
  regen_per_tick: 1           # 每秒回复量
  replace_commander_card: true # 是否发O5卡
```

---

## 🛠️ 开发：如何自己编译？

需要安装 [.NET Framework 4.8 SDK](https://dotnet.microsoft.com/download/dotnet-framework/net48)。

```bash
# 安装依赖并编译全部插件
cd "SCPSL pulint"
dotnet restore
dotnet build -c Release
```

编译好的 DLL 在 `项目文件夹\bin\Release\net48\` 下。

---

## 📁 文件结构

```
SCPSL pulint/
├── ExperiencePlugin/         ← 经验等级系统
├── ChatPlugin/               ← 聊天系统
├── CleanupPlugin/            ← 掉落物清理
├── CommanderShieldPlugin/    ← NTF指挥官护盾
├── AntiTeamKillPlugin/       ← 反组杀 + 警告
├── MyFirstPlugin/            ← EXILED插件模板
└── zeropl/
    ├── ex/   ← 旧版 EXILED 插件
    └── la/   ← 旧版 LabAPI 插件（已弃用）
```

---

## 📋 技术信息

| 项目 | 版本 |
|------|------|
| 框架 | ExMod.Exiled 9.13.3 |
| 运行环境 | .NET Framework 4.8 |
| 语言 | C# 12.0 |
| 数据存储 | YAML (YamlDotNet) |

---

> 🌟 **有问题？** 在 GitHub 提 Issue 或找服务器管理员咨询！
