# =============================================================================
# Cleanup: Remove old SwitchBuilder.* folders
# =============================================================================
# Run this AFTER closing VS Code to remove the old (now-unused) folders.
# The solution now builds from Central.* folders.
#
# Usage (from desktop/ folder):
#   powershell -ExecutionPolicy Bypass -File cleanup-old-folders.ps1
# =============================================================================

$ErrorActionPreference = "Stop"
$base = $PSScriptRoot

Write-Host "=== Cleanup Old SwitchBuilder.* Folders ===" -ForegroundColor Cyan

$folders = Get-ChildItem -Path $base -Directory -Filter "SwitchBuilder.*"
if ($folders.Count -eq 0) {
    Write-Host "No SwitchBuilder.* folders found. Already clean!" -ForegroundColor Green
    exit 0
}

Write-Host "Found $($folders.Count) old folders to remove:"
foreach ($d in $folders) {
    Write-Host "  $($d.Name)" -ForegroundColor Yellow
}

$confirm = Read-Host "`nRemove all? (y/N)"
if ($confirm -ne "y") {
    Write-Host "Cancelled." -ForegroundColor Red
    exit 0
}

foreach ($d in $folders) {
    try {
        Remove-Item -Path $d.FullName -Recurse -Force
        Write-Host "  Removed: $($d.Name)" -ForegroundColor Green
    } catch {
        Write-Host "  FAILED: $($d.Name) - $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Also remove the old rename script since it's no longer needed
$renameScript = Join-Path $base "rename-folders.ps1"
if (Test-Path $renameScript) {
    Remove-Item $renameScript -Force
    Write-Host "  Removed: rename-folders.ps1" -ForegroundColor Green
}

Write-Host "`n=== Done! ===" -ForegroundColor Cyan
