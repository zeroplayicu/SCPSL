@echo off
cd /d "z:\codebubby\SCPSL pulint"
dotnet build "SCPplaus\SCPplaus.csproj" --configuration Release --output "zeropl\ex" 2>&1
echo EXIT_CODE:%ERRORLEVEL%
