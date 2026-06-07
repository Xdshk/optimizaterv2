# GTA5 Optimizer Build Script
# Run as Administrator

Write-Host "=== GTA5 Optimizer Build Script ===" -ForegroundColor Cyan

# 1. Восстановление пакетов
Write-Host "Restoring packages..." -ForegroundColor Yellow
dotnet restore GTA5Optimizer.sln

# 2. Сборка решения (без RuntimeIdentifier)
Write-Host "Building solution..." -ForegroundColor Yellow
dotnet build GTA5Optimizer.sln -c Release --no-restore

# 3. Публикация UI (с RuntimeIdentifier)
Write-Host "Publishing application..." -ForegroundColor Yellow
dotnet publish GTA5Optimizer\src\GTA5Optimizer.UI\GTA5Optimizer.UI.csproj -c Release -r win-x64 --self-contained true -o GTA5Optimizer\publish --no-restore

Write-Host "=== Build Complete ===" -ForegroundColor Green
Write-Host "Output: GTA5Optimizer\publish\" -ForegroundColor Cyan