@echo off
REM GTA5 Optimizer Build Script for Windows
REM Run as Administrator

echo ========================================
echo   GTA5 Optimizer Build Script
echo ========================================

REM Check for dotnet
where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo ERROR: dotnet not found in PATH
    echo Please install .NET 8 Desktop Runtime
    echo https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

REM Restore packages
echo.
echo [1/4] Restoring packages...
dotnet restore GTA5Optimizer.sln
if %ERRORLEVEL% neq 0 (
    echo ERROR: Package restore failed
    pause
    exit /b 1
)

REM Build solution (without RuntimeIdentifier)
echo.
echo [2/4] Building solution...
dotnet build GTA5Optimizer.sln -c Release --no-restore
if %ERRORLEVEL% neq 0 (
    echo ERROR: Build failed
    pause
    exit /b 1
)

REM Publish UI (with RuntimeIdentifier)
echo.
echo [3/4] Publishing application...
dotnet publish GTA5Optimizer\src\GTA5Optimizer.UI\GTA5Optimizer.UI.csproj -c Release -r win-x64 --self-contained true -o GTA5Optimizer\publish --no-restore
if %ERRORLEVEL% neq 0 (
    echo ERROR: Publish failed
    pause
    exit /b 1
)

echo.
echo ========================================
echo   Build completed successfully!
echo ========================================
echo.
echo Output: GTA5Optimizer\publish\
echo Run: GTA5Optimizer\publish\GTA5Optimizer.UI.exe
echo.
pause