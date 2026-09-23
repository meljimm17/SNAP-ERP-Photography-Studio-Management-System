using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SNAP.infrastructure.Migrations.TenantErpDb
{
    /// <inheritdoc />
    public partial class RemoveRolePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RolePermissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RolePermissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccessLevel = table.Column<int>(type: "int", nullable: false),
                    Module = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.RolePermissionId);
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "RolePermissionId", "AccessLevel", "Module", "Role" },
                values: new object[,]
                {
                    { 1, 4, 3, 1 },
                    { 2, 4, 4, 1 },
                    { 3, 4, 6, 1 },
                    { 4, 4, 7, 1 },
                    { 5, 3, 3, 2 },
                    { 6, 2, 4, 2 },
                    { 7, 3, 6, 2 },
                    { 8, 3, 7, 2 },
                    { 9, 1, 3, 3 },
                    { 10, 1, 4, 3 },
                    { 11, 1, 6, 3 },
                    { 12, 0, 7, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_Role_Module",
                table: "RolePermissions",
                columns: new[] { "Role", "Module" },
                unique: true);
        }
    }
}
