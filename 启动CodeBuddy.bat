@echo off
title SCP秘密实验室插件开发 - CodeBuddy
cd /d "Z:\codebubby\SCPSL pulint"

echo ========================================
echo  正在启动 CodeBuddy ...
echo  项目: Z:\codebubby\SCPSL pulint
echo ========================================

REM 尝试用 code 命令启动
code "Z:\codebubby\SCPSL pulint" 2>nul

if errorlevel 1 (
    REM 尝试查找 CodeBuddy 安装路径
    for /f "tokens=*" %%a in ('dir /s /b "C:\Users\Administrator\AppData\Local\Programs\*CodeBuddy*.exe" 2^>nul') do (
        start "" "%%a" "Z:\codebubby\SCPSL pulint"
        goto :launched
    )
    for /f "tokens=*" %%a in ('dir /s /b "C:\Program Files\*CodeBuddy*.exe" 2^>nul') do (
        start "" "%%a" "Z:\codebubby\SCPSL pulint"
        goto :launched
    )
    echo 未找到 CodeBuddy，请在桌面手动打开 CodeBuddy 并打开此文件夹
    pause
    exit /b 1
)

:launched
echo CodeBuddy 已启动，请切换到 CodeBuddy 窗口继续聊天~
timeout /t 2 /nobreak >nul
