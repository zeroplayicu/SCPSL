# SCP:SL 服务器插件合集

适用于 **SCP: Secret Laboratory** 服务器的 Exiled 插件合集，包含经验等级、聊天系统、物品清理和指挥官护盾等功能。

## 📋 插件清单

| 插件 | 文件 | 说明 |
|------|------|------|
| **ExperiencePlugin** | `ExperiencePlugin.dll` | 经验等级 + 连杀 + 助攻 + KDA + 效果HUD |
| **ChatPlugin** | `ChatPlugin.dll` | 全体聊天/团队聊天/效果查看/生涯数据 |
| **CleanupPlugin** | `CleanupPlugin.dll` | 掉落物 + 尸体自动清理 |
| **CommanderShieldPlugin** | 已合并入 ExperiencePlugin | NTF 指挥官量子护盾 |

---

## 🎮 ExperiencePlugin — 经验等级系统

### 功能特性

- **经验/等级系统** — 击杀、伤害、在线时长获得经验，自动升级
- **SCP-207 等级加速** — 25/50/100 级获得 ×1/×2/×3 的 SCP-207 增幅（无害版）
- **无限备弹** — 换弹自动补满（含霰弹枪），丢枪/死亡不清弹药
- **击杀连杀** — 显示 `击杀玩家 x1` → `x2` → `x3`，死亡重置
- **助攻系统** — 攻击 SCP ≥250 血队友击杀 → 助攻；攻击人类 ≥20 血队友击杀 → 助攻 + 5xp/血
- **本局 KDA** — 底部状态栏实时显示 `击杀/死亡/助攻`
- **效果常驻 HUD** — 自动显示当前活跃效果和剩余时间
- **数据持久化** — 玩家数据保存至 YAML 文件
- **NTF 指挥官护盾** — 指挥官获得 O-5 卡 + AHP 护盾（右下角蓝条）

### 配置 (`%AppData%\EXILED\Configs\Plugins\exp\config.yml`)

```yaml
# 经验
ExpPerKill: 100
ExpPerDamage: 1
BaseExpPerLevel: 100
ExpPerMinute: 10

# 无限备弹
EnableInfiniteAmmo: true

# SCP207
Scp207NoDrain: true

# 助攻
ScpAssistThreshold: 250
HumanAssistThreshold: 20
HumanAssistExpPerDamage: 5

# 界面
ShowStatusAlways: true
ShowActiveEffects: true
StatusRefreshInterval: 3
```

---

## 💬 ChatPlugin — 聊天系统

### 命令列表

| 命令 | 别名 | 说明 |
|------|------|------|
| `.bc <消息>` | `.broadcast`, `.all` | 全体聊天 — 所有玩家可见 |
| `.c <消息>` | `.team`, `.t` | 团队聊天 — 仅同阵营可见 |
| `.buff` | `.effects`, `.e` | 查看当前活跃效果和剩余时间 |
| `.info` | `.career`, `.stats` | 查看生涯数据（KD、游玩时长等） |

### 配置 (`%AppData%\EXILED\Configs\Plugins\chat\config.yml`)

```yaml
BcDuration: 5          # 全体消息显示秒数
CDuration: 5           # 团队消息显示秒数
BcPrefix: "<color=#FFD700>[全体]</color>"
CPrefix: "<color=#00BFFF>[团队]</color>"
FontSize: "18"
LogChat: true
```

---

## 🧹 CleanupPlugin — 掉落物清理

### 功能

- 每 10 秒检测掉落物数量
- ≥250 个时触发倒计时公告 + 自动清理
- 同时清理尸体（Ragdoll）
- SCP 物品白名单保护

### 配置 (`%AppData%\EXILED\Configs\Plugins\cleanup\config.yml`)

```yaml
CleanupThreshold: 250
CheckInterval: 10
CountdownSeconds: 5
CleanRagdolls: true
ProtectedItemTypes: "SCP500,SCP207,SCP268,SCP1853,SCP127,SCP1344,SCP330"
```

---

## 🛡️ NTF 指挥官量子护盾（合并入 ExperiencePlugin）

### 功能

- NTF 指挥官（九尾狐队长）自动获得量子护盾
- 指挥官卡替换为 **O-5 权限卡**
- 护盾吸收伤害（AHP 优先，HS 次级）
- 每秒自动再生 1 点
- 右下角显示蓝色护盾条

### 配置 (`exp/config.yml`)

```yaml
EnableCommanderShield: true
CommanderMaxAHP: 50
CommanderMaxHS: 100
ReplaceCommanderCard: true
ShieldRegenPerTick: 1
```

---

## 📥 部署

1. 将 `zeropl/` 目录下的所有 `.dll` 复制到 `%AppData%\EXILED\Plugins\`
2. 重启服务器
3. 插件会自动生成默认配置文件在 `%AppData%\EXILED\Configs\Plugins\` 下

## 🛠️ 编译

需要 [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

```powershell
# 编译单个插件
cd ExperiencePlugin
dotnet build -c Release

# 编译全部
.\一键备份.bat
```

## 📦 技术栈

| 项目 | 版本 |
|------|------|
| 框架 | [Exiled](https://github.com/Exiled-Team/EXILED) 9.13.3 |
| 目标 | .NET Framework 4.8 |
| 语言 | C# 12.0 |
| 数据存储 | YAML (YamlDotNet) |

## 📄 协议

本项目采用 MIT 许可证。
