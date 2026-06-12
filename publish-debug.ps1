$ErrorActionPreference = 'Stop'
$project = 'C:\Users\Burov\Downloads\majestic\GTA5Optimizer\src\GTA5Optimizer.UI\GTA5Optimizer.UI.csproj'
$out = 'C:\Users\Burov\Desktop\GTA5PubDebug'

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Path $out -Force | Out-Null

Write-Host '=== Starting publish ===' -ForegroundColor Green
& dotnet publish $project -c Release -r win-x64 --self-contained true -o $out
$exitCode = $LASTEXITCODE

Write-Host "`n=== Exit code: $exitCode ===" -ForegroundColor Yellow

Write-Host "`n=== Files in output ===" -ForegroundColor Green
$files = Get-ChildItem $out -Recurse -Force -ErrorAction SilentlyContinue
if ($files) {
    $files | ForEach-Object { $size = if($_.PSIsContainer) { '[DIR]' } else { '{0:N0} KB' -f ($_.Length/1KB) }; Write-Host "  $($_.FullName) $size" }
    Write-Host "`nTotal files: $($files.Count)" -ForegroundColor Cyan
} else {
    Write-Host '  (empty!)' -ForegroundColor Red
}

Write-Host "`n=== Checking for EXE ===" -ForegroundColor Green
$exes = Get-ChildItem $out -Filter '*.exe' -Recurse -ErrorAction SilentlyContinue
if ($exes) {
    $exes | ForEach-Object { Write-Host "  FOUND: $($_.FullName) ($([math]::Round($_.Length/1MB, 2)) MB)" -ForegroundColor Green }
} else {
    Write-Host '  NO EXE FOUND!' -ForegroundColor Red
}
