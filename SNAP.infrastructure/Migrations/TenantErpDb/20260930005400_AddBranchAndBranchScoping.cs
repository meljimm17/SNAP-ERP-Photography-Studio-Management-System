using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNAP.infrastructure.Migrations.TenantErpDb
{
    /// <inheritdoc />
    public partial class AddBranchAndBranchScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Order matters. EF generates the BranchId columns as NOT NULL before the Branches
            // table exists, which fails against any tenant database that already has rows. So:
            // create Branches, seed the one Main Branch, add every column nullable, backfill the
            // required ones to that branch, and only then tighten them to NOT NULL.
            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    BranchId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContactNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsMainBranch = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.BranchId);
                });

            // Every existing tenant - Micro or Small - conceptually operated from one location.
            // That becomes its Main Branch, and all its existing data is attributed to it. The
            // studio renames it from Branch Management afterwards.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM Branches)
                INSERT INTO Branches (BranchName, IsMainBranch, IsActive, CreatedAt)
                VALUES ('Main Branch', 1, 1, SYSUTCDATETIME());");

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "TenantUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "AuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "InventoryItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Equipment",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "PurchaseOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "ProductSales",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"DECLARE @MainBranchId INT = (SELECT TOP 1 BranchId FROM Branches WHERE IsMainBranch = 1 ORDER BY BranchId);
                UPDATE Bookings SET BranchId = @MainBranchId WHERE BranchId IS NULL;
                UPDATE InventoryItems SET BranchId = @MainBranchId WHERE BranchId IS NULL;
                UPDATE Equipment SET BranchId = @MainBranchId WHERE BranchId IS NULL;
                UPDATE Expenses SET BranchId = @MainBranchId WHERE BranchId IS NULL;
                UPDATE PurchaseOrders SET BranchId = @MainBranchId WHERE BranchId IS NULL;
                UPDATE ProductSales SET BranchId = @MainBranchId WHERE BranchId IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "BranchId",
                table: "Bookings",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BranchId",
                table: "InventoryItems",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BranchId",
                table: "Equipment",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BranchId",
                table: "Expenses",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BranchId",
                table: "PurchaseOrders",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BranchId",
                table: "ProductSales",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_BranchId",
                table: "TenantUsers",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_BranchId",
                table: "PurchaseOrders",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSales_BranchId",
                table: "ProductSales",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_BranchId",
                table: "InventoryItems",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BranchId",
                table: "Expenses",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_BranchId",
                table: "Equipment",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_BranchId",
                table: "Bookings",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_BranchId",
                table: "AuditLogs",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Branches_BranchId",
                table: "AuditLogs",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "BranchId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Branches_BranchId",
                table: "Bookings",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "BranchId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Equipment_Branches_BranchId",
                table: "Equipment",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "BranchId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Branches_BranchId",
                table: "Expenses",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "BranchId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_Branches_BranchId",
                table: "InventoryItems",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "BranchId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductSales_Branches_BranchId",
                table: "ProductSales",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "BranchId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Branches_BranchId",
                table: "PurchaseOrders",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "BranchId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenantUsers_Branches_BranchId",
                table: "TenantUsers",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "BranchId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Branches_BranchId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Branches_BranchId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Equipment_Branches_BranchId",
                table: "Equipment");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Branches_BranchId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_Branches_BranchId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductSales_Branches_BranchId",
                table: "ProductSales");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Branches_BranchId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_TenantUsers_Branches_BranchId",
                table: "TenantUsers");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_TenantUsers_BranchId",
                table: "TenantUsers");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_BranchId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductSales_BranchId",
                table: "ProductSales");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_BranchId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_BranchId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_BranchId",
                table: "Equipment");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_BranchId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_BranchId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "TenantUsers");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "ProductSales");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "AuditLogs");
        }
    }
}
