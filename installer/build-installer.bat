@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ============================================
echo   GTA5 Optimizer Installer Builder
echo ============================================
echo.

REM === Configuration ===
set VERSION=2.0.0
set SOLUTION_DIR=..\GTA5Optimizer
set PROJECT_DIR=%SOLUTION_DIR%\src\GTA5Optimizer.UI
set PUBLISH_DIR=%SOLUTION_DIR%\publish
set OUTPUT_DIR=..\Output
set ISS_FILE=GTA5Optimizer.iss
set SEVEN_ZIP="C:\Program Files\7-Zip\7z.exe"
set ISCC="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

REM === Check prerequisites ===
echo [ Prerequisites Check ]

where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] .NET SDK not found. Install from https://dotnet.microsoft.com/download
    goto :error
)
echo   [OK] .NET SDK

if not exist %ISCC% (
    echo [WARN] Inno Setup 6 not found at default location.
    echo        Download from https://jrsoftware.org/isdl.php
    echo        Portable installer will still be created.
    set ISCC=
) else (
    echo   [OK] Inno Setup 6
)

if exist %SEVEN_ZIP% (
    echo   [OK] 7-Zip
    set HAS_7ZIP=1
) else (
    echo   [WARN] 7-Zip not found. Portable zip will use PowerShell.
    set HAS_7ZIP=
)

echo.
echo ============================================
echo   Building Application
echo ============================================
echo.

REM === Clean previous build ===
echo [1/5] Cleaning previous build...
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
mkdir "%OUTPUT_DIR%" 2>nul

REM === Restore packages ===
echo [2/5] Restoring NuGet packages...
cd /d "%SOLUTION_DIR%"
dotnet restore GTA5Optimizer.sln --verbosity quiet
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Package restore failed
    goto :error
)

REM === Publish ===
echo [3/5] Publishing application (Release, win-x64, self-contained)...
dotnet publish "%PROJECT_DIR%\GTA5Optimizer.UI.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -o "%PUBLISH_DIR%" ^
    --verbosity quiet
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Publish failed
    goto :error
)

echo   Published: %PUBLISH_DIR%

REM === Verify publish ===
if not exist "%PUBLISH_DIR%\GTA5Optimizer.exe" (
    echo [ERROR] GTA5Optimizer.exe not found in publish output
    dir "%PUBLISH_DIR%\*.exe"
    goto :error
)

REM === Create portable zip ===
echo [4/5] Creating portable archive...
cd /d "%OUTPUT_DIR%"

if defined HAS_7ZIP (
    %SEVEN_ZIP% a -tzip -mx9 "GTA5Optimizer-v%VERSION%-portable.zip" "..\GTA5Optimizer\publish\*"
) else (
    powershell -Command "Compress-Archive -Path '..\GTA5Optimizer\publish\*' -DestinationPath 'GTA5Optimizer-v%VERSION%-portable.zip' -Force"
)

if exist "GTA5Optimizer-v%VERSION%-portable.zip" (
    echo   Created: GTA5Optimizer-v%VERSION%-portable.zip
) else (
    echo [WARN] Portable archive creation failed
)

REM === Build installer ===
echo [5/5] Building installer...
cd /d "%~dp0"

if defined ISCC (
    %ISCC% /Q "%ISS_FILE%"
    if %ERRORLEVEL% eq 0 (
        echo   Created: ..\Output\GTA5Optimizer-Setup-v%VERSION%.exe
    ) else (
        echo [WARN] Inno Setup compilation failed. Portable version is still available.
    )
) else (
    echo   [SKIP] Inno Setup not available. Installer not created.
)

REM === Summary ===
echo.
echo ============================================
echo   Build Complete
echo ============================================
echo.
echo Outputs:
dir /b "%OUTPUT_DIR%"

echo.
echo File sizes:
for %%f in ("%OUTPUT_DIR%\*") do (
    set size=%%~zf
    if !size! gtr 1048576 (
        set /a mb=!size! / 1048576
        echo   %%~nxf: !mb! MB
    ) else (
        set /a kb=!size! / 1024
        echo   %%~nxf: !kb! KB
    )
)

echo.
echo ============================================
echo   Next Steps
echo ============================================
echo.
echo 1. Test the portable version:
echo    %PUBLISH_DIR%\GTA5Optimizer.exe
echo.
echo 2. Distribute:
echo    - Portable: %OUTPUT_DIR%\GTA5Optimizer-v%VERSION%-portable.zip
if defined ISCC (
    echo    - Installer: %OUTPUT_DIR%\GTA5Optimizer-Setup-v%VERSION%.exe
)
echo.
echo 3. For GitHub Release:
echo    - Tag: v%VERSION%
echo    - Attach both files above
echo.
goto :end

:error
echo.
echo [FATAL] Build failed with error %ERRORLEVEL%
exit /b 1

:end
endlocal
pause
