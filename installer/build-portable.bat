@echo off
chcp 65001 >nul
setlocal

echo ============================================
echo   GTA5 Optimizer Portable Builder
echo ============================================
echo.

set VERSION=2.0.0
set SOLUTION_DIR=..\GTA5Optimizer
set PROJECT_DIR=%SOLUTION_DIR%\src\GTA5Optimizer.UI
set PUBLISH_DIR=%SOLUTION_DIR%\publish
set OUTPUT_DIR=..\Output

echo [1/3] Cleaning...
if exist "%PUBLISH_DIR%" rdir /s /q "%PUBLISH_DIR%" 2>nul
if exist "%OUTPUT_DIR%" rdir /s /q "%OUTPUT_DIR%" 2>nul
mkdir "%OUTPUT_DIR%" 2>nul

echo [2/3] Publishing...
cd /d "%SOLUTION_DIR%"
dotnet restore GTA5Optimizer.sln --verbosity quiet
dotnet publish "%PROJECT_DIR%\GTA5Optimizer.UI.csproj" -c Release -r win-x64 --self-contained true -o "%PUBLISH_DIR%" --verbosity quiet

if %ERRORLEVEL% neq 0 (
    echo [ERROR] Build failed!
    pause
    exit /b 1
)

echo [3/3] Creating archive...
cd /d "%OUTPUT_DIR%"

REM Try 7-Zip first, fallback to PowerShell
set SEVENZIP="C:\Program Files\7-Zip\7z.exe"
if exist %SEVENZIP% (
    %SEVENZIP% a -tzip -mx9 "GTA5Optimizer-v%VERSION%-portable.zip" "..\GTA5Optimizer\publish\*"
) else (
    powershell -Command "Compress-Archive -Path '..\GTA5Optimizer\publish\*' -DestinationPath 'GTA5Optimizer-v%VERSION%-portable.zip' -Force"
)

echo.
echo ============================================
echo   Done!
echo ============================================
echo.
echo Output: %OUTPUT_DIR%\GTA5Optimizer-v%VERSION%-portable.zip
echo.
dir /b "%OUTPUT_DIR%"
echo.
pause

endlocal
