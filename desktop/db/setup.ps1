# Central Platform — Database Setup (Windows PowerShell)
# Applies all migrations and seeds default data.
# Usage: .\db\setup.ps1 [-Dsn "Host=localhost;..."]

param(
    [string]$Dsn = "Host=localhost;Port=5432;Database=switchbuilder;Username=switchbuilder;Password=switchbuilder"
)

$ErrorActionPreference = "Continue"
$MigrationsDir = Join-Path $PSScriptRoot "migrations"

Write-Host "Central Platform — Database Setup" -ForegroundColor Cyan
Write-Host "DSN: $Dsn"
Write-Host ""

if (-not (Test-Path $MigrationsDir)) {
    Write-Host "ERROR: migrations directory not found at $MigrationsDir" -ForegroundColor Red
    exit 1
}

# Parse DSN for psql
$parts = @{}
$Dsn -split ";" | ForEach-Object {
    $kv = $_ -split "=", 2
    if ($kv.Length -eq 2) { $parts[$kv[0].Trim()] = $kv[1].Trim() }
}

$env:PGHOST = $parts["Host"] ?? "localhost"
$env:PGPORT = $parts["Port"] ?? "5432"
$env:PGDATABASE = $parts["Database"] ?? "switchbuilder"
$env:PGUSER = $parts["Username"] ?? "switchbuilder"
$env:PGPASSWORD = $parts["Password"] ?? "switchbuilder"

Write-Host "Applying migrations..." -ForegroundColor Yellow
$applied = 0
Get-ChildItem "$MigrationsDir\*.sql" | Sort-Object Name | ForEach-Object {
    $name = $_.BaseName
    Write-Host "  $name... " -NoNewline
    try {
        psql -f $_.FullName -q 2>$null
        Write-Host "OK" -ForegroundColor Green
        $applied++
    } catch {
        Write-Host "SKIP" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Applied $applied migrations" -ForegroundColor Green

# Seed
$seedFile = Join-Path $PSScriptRoot "seed.sql"
if (Test-Path $seedFile) {
    Write-Host ""
    Write-Host "Seeding default data..." -ForegroundColor Yellow
    psql -f $seedFile -q 2>$null
    Write-Host "Seed complete" -ForegroundColor Green
}

Write-Host ""
Write-Host "Database setup complete!" -ForegroundColor Cyan
Write-Host "Default admin login: admin / admin (change immediately)" -ForegroundColor Yellow
