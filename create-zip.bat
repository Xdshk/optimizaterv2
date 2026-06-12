@echo off
chcp 65001 >nul
setlocal

set PUBLISH_DIR=C:\Users\Burov\Downloads\majestic\GTA5Optimizer\publish
set OUTPUT_DIR=C:\Users\Burov\Downloads\majestic\Output
set ZIP_NAME=GTA5Optimizer-v2.0.0-portable.zip
set LOG_FILE=%OUTPUT_DIR%\build.log

if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo Creating portable archive...
echo Source: %PUBLISH_DIR%
echo Output: %OUTPUT_DIR%\%ZIP_NAME%

powershell -NoProfile -Command "Compress-Archive -Path '%PUBLISH_DIR%\*' -DestinationPath '%OUTPUT_DIR%\%ZIP_NAME%' -Force" > "%LOG_FILE%" 2>&1

if exist "%OUTPUT_DIR%\%ZIP_NAME%" (
    echo SUCCESS!
    dir "%OUTPUT_DIR%\%ZIP_NAME%"
) else (
    echo FAILED!
    type "%LOG_FILE%"
)

endlocal
