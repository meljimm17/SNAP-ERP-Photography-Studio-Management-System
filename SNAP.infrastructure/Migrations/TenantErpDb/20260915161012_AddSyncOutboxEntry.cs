using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNAP.infrastructure.Migrations.TenantErpDb
{
    /// <inheritdoc />
    public partial class AddSyncOutboxEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncOutboxEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncOutboxEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncOutboxEntries_SyncedAt",
                table: "SyncOutboxEntries",
                column: "SyncedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncOutboxEntries");
        }
    }
}
