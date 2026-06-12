@echo off
chcp 65001 >nul
setlocal

set PUBLISH_DIR=C:\Users\Burov\Downloads\majestic\GTA5Optimizer\publish
set OUTPUT_DIR=C:\Users\Burov\Downloads\majestic\Output
set ZIP_NAME=GTA5Optimizer-v2.0.0-portable.zip

if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo Creating portable archive...
echo Source: %PUBLISH_DIR%
echo Output: %OUTPUT_DIR%\%ZIP_NAME%

powershell -NoProfile -Command ^
    "try { ^
        $src = '%PUBLISH_DIR%'; ^
        $dst = '%OUTPUT_DIR%\%ZIP_NAME%'; ^
        if (Test-Path $dst) { Remove-Item $dst -Force }; ^
        $files = (Get-ChildItem $src -Force).Count; ^
        Write-Host \"Files: $files\"; ^
        Compress-Archive -Path \"$src\*\" -DestinationPath $dst -Force; ^
        $s = (Get-Item $dst).Length; ^
        Write-Host \"Created: $dst ($s bytes)\"; ^
    } catch { ^
        Write-Host \"ERROR: $_\"; ^
        exit 1 ^
    }"

if %ERRORLEVEL% equ 0 (
    echo.
    echo Done!
    dir "%OUTPUT_DIR%\%ZIP_NAME%"
) else (
    echo.
    echo FAILED!
)

endlocal
