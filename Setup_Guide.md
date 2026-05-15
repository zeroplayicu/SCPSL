# SCP秘密实验室插件开发环境配置指南

## 📋 目录

1. [环境检查](#环境检查)
2. [安装 .NET SDK](#安装-net-sdk)
3. [安装开发工具](#安装开发工具)
4. [验证安装](#验证安装)
5. [快速开始](#快速开始)

---

## 🔍 环境检查

在开始之前，让我们检查您的系统环境。

### 检查是否已安装 .NET SDK

打开 **PowerShell** 或 **命令提示符**，运行以下命令：

```powershell
dotnet --version
```

**预期结果**：显示类似 `8.0.x` 的版本号

### 检查已安装的所有版本

```powershell
dotnet --list-sdks
```

---

## 📥 安装 .NET SDK

### Windows 系统

#### 方法1：官方安装程序（推荐）

1. 访问 [.NET 8.0 下载页面](https://dotnet.microsoft.com/download/dotnet/8.0)
2. 下载 **.NET SDK 8.0.x (x64)** 的安装程序
   - 文件名：`dotnet-sdk-8.0.x-win-x64.exe`
3. 运行安装程序，按照提示完成安装
4. **重启电脑**（如果需要）

#### 方法2：使用 PowerShell 安装

以管理员身份打开 PowerShell，运行：

```powershell
# 下载并安装 .NET SDK 8.0
Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile ".\dotnet-install.ps1"
.\dotnet-install.ps1 -Channel 8.0

# 添加到系统 PATH（可选）
[Environment]::SetEnvironmentVariable("PATH", $env:PATH + ";$env:USERPROFILE\.dotnet", "User")
```

### Linux 系统

#### Ubuntu / Debian

```bash
# 添加 Microsoft 包仓库
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# 安装 .NET SDK
sudo apt-get update
sudo apt-get install dotnet-sdk-8.0
```

#### CentOS / RHEL

```bash
sudo rpm --import https://packages.microsoft.com/keys/microsoft.asc
sudo sh -c 'echo -e "[packages-microsoft-com-prod]\nname=packages-microsoft-com-prod\nbaseurl=https://packages.microsoft.com/yumrepos/microsoft-rhel7-prod\nenabled=1\ngpgcheck=1\ngpgkey=https://packages.microsoft.com/keys/microsoft.asc" > /etc/yum.repos.d/dotnet.repo'
sudo yum update
sudo yum install dotnet-sdk-8.0
```

#### Arch Linux

```bash
sudo pacman -S dotnet-sdk-8.0
```

---

## 🛠️ 安装开发工具

### Visual Studio Code（推荐）

1. **下载安装**
   - 访问 [VS Code 官网](https://code.visualstudio.com/)
   - 下载 Windows 版本安装程序

2. **安装 C# 扩展**
   - 启动 VS Code
   - 按 `Ctrl + Shift + X` 打开扩展商店
   - 搜索 **"C#"**
   - 安装 **C# Dev Kit** 或 **C#** 扩展
   - 安装 **NuGet Gallery** 扩展（方便管理 NuGet 包）

3. **推荐扩展**
   ```
   - C# (ms-dotnettools.csharp)
   - C# Extensions (jchannon.csharpextensions)
   - NuGet Package Manager (jmrog.vscode-nuget-package-manager)
   - GitLens (eamodio.gitlens)
   ```

### Visual Studio 2022（可选，功能更强大）

1. **下载**
   - 访问 [Visual Studio 官网](https://visualstudio.microsoft.com/vs/)
   - 下载 Community 2022 版（免费）

2. **安装选项**
   - 选择 **".NET 桌面开发"** 工作负载
   - 确保包含 **.NET 8.0 Runtime**

3. **打开项目**
   - 打开解决方案文件 `.sln` 即可

---

## ✅ 验证安装

### 1. 验证 .NET SDK

```powershell
dotnet --version
```

输出应类似：
```
8.0.11
```

### 2. 验证编译工具

```powershell
dotnet --list-sdks
```

输出应类似：
```
8.0.11 [C:\Program Files\dotnet\sdk]
```

### 3. 测试项目（可选）

创建并编译一个测试项目：

```powershell
# 创建新项目
dotnet new console -n TestProject

# 进入目录
cd TestProject

# 编译
dotnet build

# 运行
dotnet run
```

---

## 🚀 快速开始

### 1. 打开示例项目

```powershell
# 进入示例项目目录
cd "c:\Users\Administrator\Desktop\SCPSL pulint\MyFirstPlugin"

# 使用 VS Code 打开
code .
```

### 2. 恢复依赖包

在 VS Code 终端中运行：

```powershell
dotnet restore
```

### 3. 编译项目

```powershell
dotnet build
```

成功编译后，会在 `bin/Debug/net8.0/` 目录生成 `MyFirstPlugin.dll` 文件。

### 4. 部署插件

将 `bin/Debug/net8.0/MyFirstPlugin.dll` 复制到服务器的插件目录：

- **Windows**: `%AppData%\EXILED\Plugins\`
- **Linux**: `~/.config/EXILED/Plugins/`

### 5. 测试运行

启动SCP服务器，检查插件是否正常加载。

---

## 📚 常见问题

### Q1: dotnet 命令找不到

**解决方案**：
1. 重启终端或电脑
2. 确认 .NET SDK 安装成功
3. 检查系统 PATH 是否包含 .NET 路径

### Q2: 编译报错 "找不到 Exiled.API"

**解决方案**：
1. 确保网络连接正常（需要下载 NuGet 包）
2. 运行 `dotnet restore`
3. 检查 .csproj 文件中的版本号是否正确

### Q3: .NET SDK 版本不兼容

**解决方案**：
1. 确认安装的是 .NET 8.0 SDK
2. 检查项目文件中的 `TargetFramework` 设置为 `net8.0`

### Q4: VS Code 不识别项目

**解决方案**：
1. 安装 C# 扩展
2. 打开文件夹（不是单个文件）
3. 信任工作区

---

## 🔗 相关链接

- [.NET 官方下载](https://dotnet.microsoft.com/download/dotnet/8.0)
- [VS Code 官方下载](https://code.visualstudio.com/)
- [EXILED 官方文档](https://exiled-team.github.io/)
- [EXILED GitHub](https://github.com/Exiled-Team/EXILED)

---

**最后更新**: 2026年5月15日
