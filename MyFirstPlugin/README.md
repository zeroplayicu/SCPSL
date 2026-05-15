# MyFirstPlugin - SCP秘密实验室示例插件

这是一个基于EXILED框架开发的SCP秘密实验室插件示例。

## 📦 功能特性

- ✅ 玩家加入时显示自定义欢迎消息
- ✅ 回合开始时记录日志
- ✅ 完整的配置系统
- ✅ 调试模式支持

## 🛠️ 开发环境

- .NET SDK 8.0+
- Visual Studio Code 或 Visual Studio 2022

## 📥 安装依赖

```bash
dotnet restore
```

## 🔨 编译

```bash
dotnet build
```

编译后的DLL文件位于 `bin/Debug/net8.0/` 或 `bin/Release/net8.0/` 目录。

## 📂 部署

将生成的DLL文件复制到：

- **Windows**: `%AppData%\EXILED\Plugins\`
- **Linux**: `~/.config/EXILED/Plugins/`

## ⚙️ 配置

插件运行后会自动在 `EXILED/Configs/` 目录生成配置文件。

## 📚 学习资源

详细的开发教程请查看根目录的 `SCPSL_Plugin_Tutorial.md` 文件。

## 📄 许可证

MIT License
