# SCP Secret Laboratory Plugin Development Environment Check Script

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SCPSL Plugin Development Environment Check" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$allChecksPassed = $true

# Check 1: .NET SDK
Write-Host "[Check 1/4] .NET SDK" -ForegroundColor Yellow
$dotnetVersion = & dotnet --version 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  [OK] .NET SDK installed: $dotnetVersion" -ForegroundColor Green
    
    # Check version
    if ($dotnetVersion -like "8.*") {
        Write-Host "  [OK] Version meets requirements (.NET 8.0)" -ForegroundColor Green
    } else {
        Write-Host "  [WARN] .NET 8.0 recommended (current: $dotnetVersion)" -ForegroundColor Yellow
    }
} else {
    Write-Host "  [FAIL] .NET SDK not installed or not in PATH" -ForegroundColor Red
    Write-Host "  -> Please install .NET 8.0 SDK per Setup_Guide.md" -ForegroundColor Yellow
    $allChecksPassed = $false
}

Write-Host ""

# Check 2: NuGet
Write-Host "[Check 2/4] NuGet Package Manager" -ForegroundColor Yellow
$nugetCheck = & dotnet nuget --version 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  [OK] NuGet installed: $nugetCheck" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] NuGet not available" -ForegroundColor Red
    $allChecksPassed = $false
}

Write-Host ""

# Check 3: Example project files
Write-Host "[Check 3/4] Example Project Files" -ForegroundColor Yellow
$projectPath = ".\MyFirstPlugin"
if (Test-Path $projectPath) {
    Write-Host "  [OK] Project directory exists" -ForegroundColor Green
    
    if (Test-Path ".\MyFirstPlugin\MyFirstPlugin.csproj") {
        Write-Host "  [OK] Project file (.csproj) exists" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] Project file (.csproj) not found" -ForegroundColor Red
        $allChecksPassed = $false
    }
    
    if (Test-Path ".\MyFirstPlugin\Plugin.cs") {
        Write-Host "  [OK] Main plugin class (Plugin.cs) exists" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] Main plugin class (Plugin.cs) not found" -ForegroundColor Red
        $allChecksPassed = $false
    }
} else {
    Write-Host "  [FAIL] Project directory not found" -ForegroundColor Red
    $allChecksPassed = $false
}

Write-Host ""

# Check 4: Documentation files
Write-Host "[Check 4/4] Documentation Files" -ForegroundColor Yellow
$docsExist = $true

if (Test-Path ".\SCPSL_Plugin_Tutorial.md") {
    Write-Host "  [OK] Plugin Tutorial (SCPSL_Plugin_Tutorial.md)" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] Plugin Tutorial not found" -ForegroundColor Red
    $docsExist = $false
}

if (Test-Path ".\Setup_Guide.md") {
    Write-Host "  [OK] Setup Guide (Setup_Guide.md)" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] Setup Guide not found" -ForegroundColor Red
    $docsExist = $false
}

if (-not $docsExist) {
    $allChecksPassed = $false
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan

# Summary
if ($allChecksPassed) {
    Write-Host "  [OK] All checks passed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Next Steps:" -ForegroundColor Yellow
    Write-Host "  1. cd MyFirstPlugin" -ForegroundColor White
    Write-Host "  2. dotnet restore    # Download dependencies" -ForegroundColor White
    Write-Host "  3. dotnet build     # Build plugin" -ForegroundColor White
} else {
    Write-Host "  [FAIL] Some checks failed, please fix issues above" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Refer to Setup_Guide.md for environment setup" -ForegroundColor Yellow
}

Write-Host "========================================" -ForegroundColor Cyan
