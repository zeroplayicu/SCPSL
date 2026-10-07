# SCP秘密实验室插件开发 - 对话记录导出
> 导出时间: 2026-05-22 16:54
> 项目路径: Z:\codebubby\SCPSL pulint
> 框架: ExMod.Exiled 9.13.3（已从 LabAPI 迁移至 EXILED）

## 📋 已完成插件清单

### 1. ExperiencePlugin - 经验等级系统插件
**源码**: `ExperiencePlugin/`
**功能**:
- ✅ 底部状态栏: `━━ 玩家名 | Lv.1 75/100 | 0h30m ━━`
- ✅ 准星下方战斗反馈: "造成伤害: 15xp" + "击杀玩家: +100xp"
- ✅ 伤害经验延迟5秒结算
- ✅ 等级系统(公式: 基础经验×等级)
- ✅ 击杀100经验, 每点伤害1经验
- ✅ MovementBoost移速增幅(替代SCP207,不掉血)
- ✅ 25级移速70 / 50级移速120 / 100级移速200
- ✅ D级开局发清洁工卡
- ✅ 博士/保安/MTF等级加速(25级起)
- ✅ 死亡不掉弹药(Dying事件清空)
- ✅ 无限备弹(换弹满弹+1,跳过SCP127)
- ✅ SCP500不取消加速(MovementBoost是正面效果)
- ✅ 数据YAML持久化: `%AppData%\EXILED\ExperienceData\`

### 2. CleanupPlugin - 掉落物自动清理插件
**源码**: `CleanupPlugin/`
**功能**:
- ✅ 每10秒检测掉落物数量
- ✅ ≥250个触发 → 全屏"我要扫地了抬抬脚" + 5秒倒计时
- ✅ 倒计时结束自动清空所有掉落物

### 3. ChatPlugin - 全体聊天(BC) + 团队聊天(C) 插件
**源码**: `ChatPlugin/`
**功能**:
- ✅ `.bc <消息>` → 全体聊天，所有玩家可见（金色`[全体]`前缀）
- ✅ `.c <消息>` → 团队聊天，仅同阵营玩家可见（蓝色`[团队]`前缀）
- ✅ `.buff` / `.e` → 查看当前效果
- ✅ `.info` / `.stats` → 查看生涯数据
- ✅ 支持命令别名: `.broadcast` / `.all` (BC), `.team` / `.t` (C)
- ✅ 可配置消息颜色、字体大小、显示时长
- ✅ 服务器日志记录聊天内容

### 4. AntiTeamKillPlugin - 反组杀 + 警告系统
**源码**: `AntiTeamKillPlugin/`
**功能**:
- ✅ 攻击队友扣HP + 扣经验
- ✅ 击杀队友扣150XP + 超过阈值自动处罚为Tutorial
- ✅ `.AC` 玩家向管理员发送消息
- ✅ `.ma` 被组杀后变教程角色
- ✅ `.warr` 警告查看/管理
- ✅ 管理员消息轮播系统

### 5. CommanderShieldPlugin - NTF指挥官量子护盾
**源码**: `CommanderShieldPlugin/`
**功能**:
- ✅ NTF指挥官生成获得50AHP + 100HS
- ✅ 攻击SCP/人类回复护盾
- ✅ 指挥官卡替换为O5权限卡
- ✅ 实时HUD进度条显示

### 6. MyFirstPlugin - EXILED模板示例
**源码**: `MyFirstPlugin/`

## 📁 编译产物目录
`zeropl/ex/` - EXILED 框架 DLL
`zeropl/la/` - 旧版 LabAPI 框架 DLL（已弃用）

## 🔧 技术参数
- 框架: ExMod.Exiled 9.13.3
- 目标: .NET Framework 4.8
- 语言: C# 12.0
- 数据: YAML

## 📥 部署路径
DLL放入: `%AppData%\EXILED\Plugins\`
配置: `%AppData%\EXILED\Configs\`
数据: `%AppData%\EXILED\ExperienceData\`

## ⚠️ 变更记录
- 2026-05-22: 全部插件从 LabAPI 1.1.6.1 迁移至 ExMod.Exiled 9.13.3


---

## 2026-05-24 更新 - 环境搭建 + 全部编译

### 环境
- .NET 8.0 SDK (8.0.421) + .NET Framework 4.8.1 DevPack
- 创建 SCPSLPlugins.sln + Directory.Build.props
- NuGet: .csproj 改为 ExMod.Exiled 9.13.3

### API 适配 (EXILED 9.13.3)
- Target -> Player / IsNpc -> IsNPC / DisplayName -> Nickname
- Delete() -> Destroy() / CurrentRound -> UptimeRounds
- IConfig 新增 Debug / ItemType -> AmmoType

### 结果
- 6 插件全部编译成功 0 错误 0 警告
- DLL 输出到 zeropl/ex/
- 服务端: L:\\SteamLibrary\\steamapps\\common\\SCP Secret Laboratory Dedicated Server


---

## 2026-05-24 阵营插件开发

### 删除
- CommanderShieldPlugin (被FactionPlugin替代)

### 新增: FactionPlugin (31 KB, 0错误)

**设施主管**: NTF指挥官皮, E11-SR 62/52/42/35伤害, 广播室技能呼叫快反
**GOC小队(6人)**: 指挥官+重装+先锋+士兵×3, CASSIE入场, 限制MTF一波
**撤离系统**: 撤离→观察者+1000XP (读取ExperiencePlugin数据)
**命令**: .qt / .goc / .skill(.jn) / js sszg

### zeropl/ex 当前DLL
AntiTeamKill(22.5K) Chat(23K) Cleanup(11K) Experience(41.5K) Faction(31K) MyFirst(11K)
