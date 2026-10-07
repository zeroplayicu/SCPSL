@echo off
cd /d "z:\codebubby\SCPSL pulint"
echo Building ExperiencePlugin...
dotnet build "ExperiencePlugin\ExperiencePlugin.csproj" --configuration Release --output "zeropl\ex" 2>&1
echo EXIT_CODE:%ERRORLEVEL%
echo Building ExpWebBate...
dotnet build "ExpWebBate\ExpWebBate.csproj" --configuration Release --output "zeropl\ex" 2>&1
echo EXIT_CODE:%ERRORLEVEL%
echo Done.
