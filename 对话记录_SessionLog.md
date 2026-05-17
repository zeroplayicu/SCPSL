# SCP秘密实验室插件开发 - 对话记录导出
> 导出时间: 2026-05-15 15:59
> 项目路径: Z:\codebubby\SCPSL pulint
> 提醒: 网吧环境下，请用"一键备份.bat"保存所有数据

## 📋 已完成插件清单

### 1. ExperiencePlugin - 经验等级系统插件
**编译**: `zeropl/ExperiencePlugin.dll`
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
**编译**: `zeropl/CleanupPlugin.dll`
**源码**: `CleanupPlugin/`
**功能**:
- ✅ 每10秒检测掉落物数量
- ✅ ≥250个触发 → 全屏"我要扫地了抬抬脚" + 5秒倒计时
- ✅ 倒计时结束自动清空所有掉落物

### 3. MyFirstPlugin - 模板示例
**源码**: `MyFirstPlugin/`

### 4. ChatPlugin - 全体聊天(BC) + 团队聊天(C) 插件
**编译**: `zeropl/ChatPlugin.dll`
**源码**: `ChatPlugin/`
**功能**:
- ✅ `.bc <消息>` → 全体聊天，所有玩家可见（金色`[全体]`前缀）
- ✅ `.c <消息>` → 团队聊天，仅同阵营玩家可见（蓝色`[团队]`前缀）
- ✅ 支持命令别名: `.broadcast` / `.all` (BC), `.team` / `.t` (C)
- ✅ 可配置消息颜色、字体大小、显示时长
- ✅ 服务器日志记录聊天内容
- ✅ 使用 Exiled 客户端命令系统，输入框直接输入命令

## 📁 编译产物目录
`zeropl/`:
- `ExperiencePlugin.dll` (27KB)
- `CleanupPlugin.dll` (9KB)
- `ChatPlugin.dll` (11KB)
- `Exiled/` - EXILED 9.13.3 框架DLL

## 🔧 技术参数
- 框架: ExMod.Exiled 9.13.3
- 目标: .NET Framework 4.8
- 语言: C# 12.0
- 数据: YAML

## 📥 部署路径
DLL放入: `%AppData%\EXILED\Plugins\`
配置: `%AppData%\EXILED\Configs\Plugins\exp\`
数据: `%AppData%\EXILED\ExperienceData\`

## ⚠️ 下次使用
1. 双击 `启动CodeBuddy.bat` 打开项目
2. 如果CodeBuddy对话记录丢失，可根据本文档内容重新开发
3. 建议先运行 `一键备份.bat` 保存所有数据到U盘/云盘
