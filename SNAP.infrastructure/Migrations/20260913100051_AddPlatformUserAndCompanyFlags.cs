using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNAP.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformUserAndCompanyFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "isActive",
                table: "Companies",
                newName: "IsActive");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "CompanyDatabases",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Companies",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.CreateTable(
                name: "PlatformUsers",
                columns: table => new
                {
                    PlatformUserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformUsers", x => x.PlatformUserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformUsers_Username",
                table: "PlatformUsers",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformUsers");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "Companies",
                newName: "isActive");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "CompanyDatabases",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "isActive",
                table: "Companies",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);
        }
    }
}
