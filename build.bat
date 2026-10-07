@echo off
cd /d "C:\Users\Administrator\Desktop\SCPSL pulint\ExperiencePlugin"
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" -nologo -target:library -out:"C:\Users\Administrator\Desktop\SCPSL pulint\zeropl\ex\ExperiencePlugin.dll" -platform:anycpu -langversion:latest -reference:..\Dependencies\HintServiceMeow-Exiled.dll -reference:bin\Debug\net48\Exiled.API.dll -reference:bin\Debug\net48\Exiled.Events.dll -reference:bin\Debug\net48\Exiled.Loader.dll -reference:bin\Debug\net48\Exiled.Permissions.dll -reference:bin\Debug\net48\Exiled.CreditTags.dll -reference:bin\Debug\net48\Exiled.CustomItems.dll -reference:bin\Debug\net48\Exiled.CustomRoles.dll -reference:bin\Debug\net48\YamlDotNet.dll -reference:bin\Debug\net48\Assembly-CSharp-Publicized.dll -reference:bin\Debug\net48\UnityEngine.dll -reference:bin\Debug\net48\UnityEngine.CoreModule.dll -reference:bin\Debug\net48\CommandSystem.Core.dll -reference:bin\Debug\net48\NorthwoodLib.dll -reference:bin\Debug\net48\LabApi.dll CombatData.cs ExperienceConfig.cs ExperienceEventHandler.cs ExperiencePlugin.cs PlayerData.cs PlayerDataManager.cs CdkManager.cs LotteryManager.cs ScpSelectManager.cs SssSettings.cs WarningManager.cs Commands\BcCommand.cs Commands\CCommand.cs Commands\CdkAddCommand.cs Commands\CdkCommand.cs Commands\ChatMessageBuffer.cs Commands\LotteryCommand.cs Commands\ScpCommand.cs Commands\SettingsCommand.cs Commands\VipBadgeCommand.cs Commands\WarnRaCommand.cs > build.log 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo BUILD FAILED
    type build.log
    exit /b 1
)
echo BUILD SUCCESS
