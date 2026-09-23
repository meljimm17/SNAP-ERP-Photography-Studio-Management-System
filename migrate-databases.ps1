# Database Migration Script for SNAP ERP
# This script applies Entity Framework migrations to both databases
# Run this script with: powershell -ExecutionPolicy Bypass -File migrate-databases.ps1

Write-Host "╔════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     SNAP ERP Database Migration Script                ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Check if dotnet is available
try {
	$dotnetVersion = dotnet --version
	Write-Host "✓ .NET SDK found: $dotnetVersion" -ForegroundColor Green
} catch {
	Write-Host "✗ dotnet CLI not found. Please install .NET SDK." -ForegroundColor Red
	exit 1
}

$scriptDir = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition
Set-Location $scriptDir

Write-Host "📦 Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore

Write-Host ""
Write-Host "🔧 Building project..." -ForegroundColor Yellow
dotnet build -c Release

if ($LASTEXITCODE -ne 0) {
	Write-Host "✗ Build failed!" -ForegroundColor Red
	exit 1
}

Write-Host ""
Write-Host "════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "📊 Applying Migrations to SNAP ERP Databases" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Navigate to infrastructure project directory
$infrastructureDir = Join-Path $scriptDir "SNAP.infrastructure"
$winformsDir = Join-Path $scriptDir "SNAP"

Write-Host "🚀 Applying Master ERP migrations..." -ForegroundColor Yellow
Write-Host "   Database: db67905 (DB_MasterERP)" -ForegroundColor Gray

# Note: For Azure SQL, we need to ensure the context is explicitly specified
Set-Location $infrastructureDir

dotnet ef database update `
	--context MasterErpDbContext `
	--startup-project $winformsDir `
	--configuration Release

if ($LASTEXITCODE -ne 0) {
	Write-Host "✗ Failed to apply Master migrations!" -ForegroundColor Red
	exit 1
}

Write-Host ""
Write-Host "🚀 Applying Tenant ERP migrations..." -ForegroundColor Yellow
Write-Host "   Database: db67904 (tenant_a)" -ForegroundColor Gray

dotnet ef database update `
	--context TenantErpDbContext `
	--startup-project $winformsDir `
	--configuration Release

if ($LASTEXITCODE -ne 0) {
	Write-Host "✗ Failed to apply Tenant migrations!" -ForegroundColor Red
	exit 1
}

Write-Host ""
Write-Host "════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✅ Migration complete!" -ForegroundColor Green
Write-Host "════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "📋 What's next:" -ForegroundColor Cyan
Write-Host "   1. Open Visual Studio and run the SNAP application" -ForegroundColor Gray
Write-Host "   2. Login with your company's credentials (check Companies.CompanyCode in MasterErp)" -ForegroundColor Gray
Write-Host ""
