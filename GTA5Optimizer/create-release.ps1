# Create Release Package for GTA5 Optimizer
param(
    [string]$Configuration = "Release",
    [string]$Platform = "win-x64"
)

Write-Host "=== Creating GTA5 Optimizer Release ===" -ForegroundColor Cyan

# Build
Write-Host "Building..." -ForegroundColor Yellow
dotnet build GTA5Optimizer.sln -c $Configuration --no-restore

# Publish
Write-Host "Publishing..." -ForegroundColor Yellow
dotnet publish GTA5Optimizer\src\GTA5Optimizer.UI\GTA5Optimizer.UI.csproj -c $Configuration -r $Platform --self-contained true -o GTA5Optimizer\publish --no-restore

# Create ZIP
Write-Host "Creating ZIP..." -ForegroundColor Yellow
Compress-Archive -Path GTA5Optimizer\publish\* -DestinationPath GTA5Optimizer_Release_1.0.0.zip -Force

Write-Host "=== Release created: GTA5Optimizer_Release_1.0.0.zip ===" -ForegroundColor Green