#!/bin/bash
# Database Migration Script for SNAP ERP
# This script applies Entity Framework migrations to both databases

echo "╔════════════════════════════════════════════════════════╗"
echo "║     SNAP ERP Database Migration Script                ║"
echo "╚════════════════════════════════════════════════════════╝"
echo ""

# Check if dotnet is available
if ! command -v dotnet &> /dev/null; then
	echo "❌ dotnet CLI not found. Please install .NET SDK."
	exit 1
fi

cd "$(dirname "$0")"

echo "📦 Restoring NuGet packages..."
dotnet restore

echo ""
echo "🔧 Building project..."
dotnet build -c Release

echo ""
echo "════════════════════════════════════════════════════════"
echo "📊 Applying Migrations to SNAP ERP Databases"
echo "════════════════════════════════════════════════════════"
echo ""

# Navigate to infrastructure project
cd SNAP.infrastructure

echo "🚀 Applying Master ERP migrations to db67905..."
dotnet ef database update --startup-project ..\SNAP\SNAP.winforms.csproj --configuration Release

echo ""
echo "🚀 Applying Tenant ERP migrations to db67904..."
dotnet ef database update --startup-project ..\SNAP\SNAP.winforms.csproj --configuration Release

echo ""
echo "✅ Migration complete!"
echo ""
echo "You can now run the SNAP ERP application."
