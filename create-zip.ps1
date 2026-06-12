[CmdletBinding()]
param()

$publishDir = 'C:\Users\Burov\Downloads\majestic\GTA5Optimizer\publish'
$outputDir = 'C:\Users\Burov\Downloads\majestic\Output'
$zipName = 'GTA5Optimizer-v2.0.0-portable.zip'
$zipPath = Join-Path $outputDir $zipName

if (-not (Test-Path $publishDir)) {
    Write-Error "Publish directory not found: $publishDir"
}
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$exePath = Join-Path $publishDir 'GTA5Optimizer.exe'
if (-not (Test-Path $exePath)) {
    Write-Error "GTA5Optimizer.exe not found!"
}

$files = Get-ChildItem -Path $publishDir -Force
Write-Host "Files to archive: $($files.Count)"
$totalSize = ($files | Measure-Object -Property Length -Sum).Sum
Write-Host "Uncompressed size: $([math]::Round($totalSize/1MB, 2)) MB"

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($publishDir, $zipPath, 'Optimal', $false)

if (Test-Path $zipPath) {
    $zipSize = (Get-Item $zipPath).Length
    Write-Host ""
    Write-Host "SUCCESS: $zipPath"
    Write-Host "Archive size: $([math]::Round($zipSize/1MB, 2)) MB"
} else {
    Write-Error "Archive creation failed!"
}
