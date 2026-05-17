# Experience Plugin - SCP秘密实验室经验等级系统插件

## 📝 功能介绍

这是一个功能完善的 **EXILED 框架插件**，为 SCP 秘密实验室服务器提供完整的玩家经验和等级系统。

### 🎯 核心功能

- ✅ **玩家等级系统** - 玩家可以通过游戏获得经验值提升等级
- ✅ **经验值累积** - 支持多种途径获得经验（游玩时间、击杀等）
- ✅ **游玩时长统计** - 自动记录玩家的累计游戏时长
- ✅ **数据持久化** - 所有数据保存在 YAML 文件中，重启服务器不丢失
- ✅ **状态提示面板** - 玩家加入时在屏幕中间下方显示个人状态信息

---

## 📊 状态面板显示内容

玩家进入游戏后，会在 **屏幕中间下方** 显示类似以下内容：

```
        欢迎回来，玩家名称
        
        等级: Lv.5
        经验: 75/500
        游玩时长: 2小时30分钟
```

---

## ⚙️ 配置说明

插件配置文件位于：`EXILED/Configs/[端口号]-config.yml`

### 基础配置项

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `is_enabled` | bool | true | 是否启用插件 |
| `debug_mode` | bool | false | 调试模式开关 |
| `experience_per_minute` | int | 10 | 每分钟游戏获得的经验值 |
| `experience_per_kill` | int | 50 | 击杀其他玩家获得的经验值 |
| `experience_per_death` | int | 0 | 死亡扣除的经验值（0为不扣除） |
| `show_status_on_join` | bool | true | 加入时是否显示状态提示 |
| `status_display_duration` | ushort | 10 | 状态提示显示时长（秒） |
| `level_prefix` | string | "Lv." | 等级前缀显示 |

### 状态消息配置

```yaml
status_message: |
  <size=25><color=yellow>欢迎回来，{player}</color></size>
  <size=20><color=cyan>等级: </color><color=white>{level}</color></size>
  <size=20><color=cyan>经验: </color><color=white>{exp}/{maxexp}</color></size>
  <size=20><color=cyan>游玩时长: </color><color=white>{time}</color></size>
```

### 可用占位符

| 占位符 | 说明 | 示例 |
|--------|------|------|
| `{player}` | 玩家名称 | 张三 |
| `{level}` | 当前等级 | Lv.5 |
| `{exp}` | 当前经验值 | 75 |
| `{maxexp}` | 升级所需经验 | 500 |
| `{time}` | 游玩时长 | 2小时30分钟 |

---

## 📂 数据存储

### 玩家数据文件

- **位置**: `EXILED/Configs/ExperienceData/player_data.yml`
- **格式**: YAML
- **内容**: 所有玩家的经验和等级数据

### 数据结构

```yaml
userId_123:
  userId: "userId_123"
  playerName: "张三"
  experience: 75
  level: 5
  totalPlayTimeMinutes: 150
  totalKills: 23
  totalDeaths: 15
  lastLoginTime: "2026-05-15T12:30:00"
  createdTime: "2026-05-10T08:00:00"
```

---

## 🎮 经验获取方式

| 方式 | 经验值 | 说明 |
|------|--------|------|
| 游玩时间 | 每分钟 10 点 | 回合结束时根据实际游玩时长计算 |
| 击杀玩家 | 50 点 | 击杀其他玩家时立即获得 |
| 死亡惩罚 | 0 点 | 默认不扣除，可在配置中启用 |

### 升级公式

```
升级所需经验 = 基础经验值 × 当前等级
```

**示例**（基础经验 = 100）：
- 1级 → 2级：需要 100 × 1 = 100 经验
- 5级 → 6级：需要 100 × 5 = 500 经验
- 10级 → 11级：需要 100 × 10 = 1000 经验

---

## 🛠️ 安装步骤

### 方法一：编译项目

1. **编译插件**
   ```bash
   cd ExperiencePlugin
   dotnet restore
   dotnet build
   ```

2. **复制 DLL 文件**
   ```
   bin/Debug/net8.0/ExperiencePlugin.dll
   ```
   复制到：
   - **Windows**: `%AppData%\EXILED\Plugins\`
   - **Linux**: `~/.config/EXILED/Plugins/`

### 方法二：使用预编译版本

（如果有预编译版本）直接将 DLL 文件放入插件目录即可。

---

## 🔧 使用方法

### 首次使用

1. 将 DLL 文件放入插件目录
2. 重启服务器
3. 插件会自动创建配置文件
4. 玩家加入时即可看到状态提示

### 配置自定义

1. 编辑服务器配置文件：
   ```
   %AppData%\EXILED\Configs\[端口号]-config.yml
   ```

2. 找到 `experience_plugin` 部分

3. 修改配置后重启服务器或使用重载命令

---

## 📝 使用说明

### 玩家操作

- **加入游戏**: 自动显示当前等级、经验、游玩时长
- **击杀玩家**: 立即获得经验提示
- **回合结束**: 根据游玩时长获得经验奖励

### 管理员操作

- **查看数据**: 直接查看 `player_data.yml` 文件
- **修改数据**: 可以手动编辑 YAML 文件修改玩家数据
- **重置数据**: 删除 `player_data.yml` 文件，重启后所有玩家数据重置

---

## 🔍 调试模式

启用调试模式后会输出详细的日志信息：

- 玩家数据加载/保存详情
- 经验获得/扣除详细记录
- 事件处理过程
- 公式计算过程

**注意**: 正常运行时建议关闭调试模式。

---

## ❓ 常见问题

### Q1: 玩家加入没有显示状态信息？

**检查项**：
1. 插件是否成功加载（查看服务器日志）
2. `show_status_on_join` 配置是否为 `true`
3. 配置文件格式是否正确

### Q2: 经验值没有累积？

**检查项**：
1. 回合是否正常结束（需要回合结束才会更新游玩时长）
2. 配置文件中的 `experience_per_minute` 值是否为正数

### Q3: 如何手动修改玩家数据？

直接编辑 `player_data.yml` 文件：
```yaml
"player_userid":
  level: 10
  experience: 250
  totalPlayTimeMinutes: 500
```

### Q4: 数据存储在哪里？

`EXILED/Configs/ExperienceData/player_data.yml`

---

## 🔗 相关资源

- [EXILED 官方文档](https://exiled-team.github.io/)
- [SCP秘密实验室论坛](https://scpslgame.com/)

---

## 📝 开发信息

- **框架**: EXILED 5.3.3
- **.NET 版本**: 8.0
- **编程语言**: C#
- **数据格式**: YAML

---

## 🎉 致谢

感谢使用本插件！如有问题或建议，欢迎反馈。

---

**版本**: 1.0.0  
**更新日期**: 2026年5月15日  
**作者**: Developer
