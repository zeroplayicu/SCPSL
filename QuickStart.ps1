# SCP秘密实验室插件开发 - 快速开始脚本

# 创建示例项目目录结构

# 1. 创建主目录
Write-Host "正在创建项目目录..." -ForegroundColor Cyan

# 2. 显示项目结构
Write-Host "`n项目目录结构:" -ForegroundColor Green
Write-Host "SCPSL Plugin Project/"
Write-Host "├── MyFirstPlugin/"
Write-Host "│   ├── Plugin.cs          (主插件类)"
Write-Host "│   ├── Config.cs          (配置文件)"
Write-Host "│   ├── EventHandlers.cs   (事件处理)"
Write-Host "│   ├── MyFirstPlugin.csproj"
Write-Host "│   ├── MyFirstPlugin.sln"
Write-Host "│   └── README.md"
Write-Host "├── SCPSL_Plugin_Tutorial.md    (完整教程)"
Write-Host "├── Setup_Guide.md              (环境配置指南)"
Write-Host "└── QuickStart.ps1              (本脚本)"

# 3. 环境检查
Write-Host "`n正在检查开发环境..." -ForegroundColor Cyan

# 检查 .NET SDK
$dotnetCheck = & dotnet --version 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] .NET SDK 已安装: $dotnetCheck" -ForegroundColor Green
} else {
    Write-Host "[X] .NET SDK 未安装或未配置到 PATH" -ForegroundColor Red
    Write-Host "请参考 Setup_Guide.md 安装 .NET 8.0 SDK" -ForegroundColor Yellow
}

# 4. 编译项目
Write-Host "`n正在尝试验证项目..." -ForegroundColor Cyan
Set-Location ".\MyFirstPlugin"

if (Test-Path ".\MyFirstPlugin.csproj") {
    Write-Host "[OK] 项目文件存在" -ForegroundColor Green
    
    Write-Host "`n运行 'dotnet restore' 来下载依赖包..." -ForegroundColor Yellow
    Write-Host "运行 'dotnet build' 来编译项目..." -ForegroundColor Yellow
} else {
    Write-Host "[X] 项目文件未找到" -ForegroundColor Red
}

Set-Location ".."

# 5. 后续步骤
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "后续步骤:" -ForegroundColor Yellow
Write-Host "1. 安装 .NET 8.0 SDK（如尚未安装）"
Write-Host "2. 安装 VS Code 和 C# 扩展"
Write-Host "3. 运行 'dotnet restore' 下载依赖"
Write-Host "4. 运行 'dotnet build' 编译插件"
Write-Host "5. 将 DLL 部署到服务器 EXILED/Plugins 目录"
Write-Host "========================================"

# 6. 打开教程
$openDocs = Read-Host "`n是否打开教程文档？(Y/N)"
if ($openDocs -eq "Y" -or $openDocs -eq "y") {
    Start-Process "notepad.exe" ".\SCPSL_Plugin_Tutorial.md"
}
