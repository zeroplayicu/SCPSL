# SCP秘密实验室插件开发 - 项目说明

> 🎮 欢迎学习SCP秘密实验室插件开发！

## 📁 项目结构

```
SCPSL Plugin Project/
├── 📄 README.md                    ← 本文件
├── 📚 SCPSL_Plugin_Tutorial.md     ← 完整的插件开发教程
├── 📋 Setup_Guide.md               ← 开发环境配置指南
├── ⚡ QuickStart.ps1                ← 快速开始脚本
├── 🔍 CheckEnvironment.ps1          ← 环境检查脚本
│
└── 📦 MyFirstPlugin/               ← 示例插件项目
    ├── Plugin.cs                   ← 主插件类
    ├── Config.cs                   ← 配置文件
    ├── EventHandlers.cs            ← 事件处理类
    ├── MyFirstPlugin.csproj        ← 项目配置文件
    ├── MyFirstPlugin.sln           ← 解决方案文件
    └── README.md                   ← 项目说明
```

## 🚀 快速开始

### 1. 检查开发环境

```powershell
# 运行环境检查脚本
.\CheckEnvironment.ps1
```

### 2. 安装 .NET SDK（如需要）

参考 `Setup_Guide.md` 文档安装 .NET 8.0 SDK

### 3. 编译示例插件

```powershell
# 进入项目目录
cd MyFirstPlugin

# 下载依赖包
dotnet restore

# 编译项目
dotnet build
```

### 4. 部署插件

将 `bin/Debug/net8.0/MyFirstPlugin.dll` 复制到服务器插件目录：
- Windows: `%AppData%\EXILED\Plugins\`
- Linux: `~/.config/EXILED/Plugins/`

## 📚 学习路径

### 入门阶段
1. 阅读 `SCPSL_Plugin_Tutorial.md` 了解插件开发基础
2. 阅读 `Setup_Guide.md` 配置开发环境
3. 分析 `MyFirstPlugin` 示例项目代码
4. 尝试修改示例代码并重新编译

### 进阶阶段
1. 学习EXILED事件系统
2. 实现更复杂的功能
3. 阅读优秀开源插件源码
4. 参与社区交流

## 🔗 相关资源

- [EXILED 官方文档](https://exiled-team.github.io/)
- [EXILED GitHub](https://github.com/Exiled-Team/EXILED)
- [SCP秘密实验室论坛](https://scpslgame.com/)
- [.NET 官方文档](https://docs.microsoft.com/dotnet/)

## 📝 常用命令

```powershell
# 恢复依赖
dotnet restore

# 编译
dotnet build

# 发布Release版本
dotnet publish -c Release

# 清理编译输出
dotnet clean

# 运行测试
dotnet test
```

## ⚠️ 注意事项

1. **服务器路径**：确保SCP服务器路径不包含中文字符
2. **EXILED版本**：确保插件版本与服务器EXILED版本匹配
3. **.NET版本**：开发时使用 .NET 8.0 SDK
4. **备份**：修改代码前先备份原文件

## 💡 常见问题

Q: 编译报错找不到Exiled.API？
A: 运行 `dotnet restore` 下载NuGet包

Q: 插件不生效？
A: 检查配置文件中的 `is_enabled` 是否为 true

Q: 如何调试？
A: 启用配置中的 `DebugMode`，查看服务器日志

## 🎯 下一步

1. ✅ 阅读完整教程 `SCPSL_Plugin_Tutorial.md`
2. ✅ 配置开发环境 `Setup_Guide.md`
3. ✅ 编译并测试示例插件
4. ✅ 开始编写自己的插件！

---

**祝你开发愉快！** 🎮

*最后更新：2026年5月15日*
