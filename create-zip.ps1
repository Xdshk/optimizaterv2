$ErrorActionPreference = 'Stop'
$publishDir = 'C:\Users\Burov\Downloads\majestic\GTA5Optimizer\publish'
$outputDir = 'C:\Users\Burov\Downloads\majestic\Output'
$zipName = 'GTA5Optimizer-v2.0.0-portable.zip'
$zipPath = Join-Path $outputDir $zipName

if (-not (Test-Path $publishDir)) {
    Write-Error "Publish directory not found: $publishDir"
    exit 1
}

$exePath = Join-Path $publishDir 'GTA5Optimizer.exe'
if (-not (Test-Path $exePath)) {
    Write-Error "GTA5Optimizer.exe not found in publish output!"
    exit 1
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

$files = Get-ChildItem $publishDir
Write-Host "Files to archive: $($files.Count)"
$totalSize = ($files | Measure-Object -Property Length -Sum).Sum
Write-Host "Total size: $([math]::Round($totalSize/1MB, 2)) MB"

Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force

if (Test-Path $zipPath) {
    $zipSize = (Get-Item $zipPath).Length
    Write-Host "SUCCESS: $zipPath ($([math]::Round($zipSize/1MB, 2)) MB)"
} else {
    Write-Error "Archive creation failed!"
    exit 1
}
