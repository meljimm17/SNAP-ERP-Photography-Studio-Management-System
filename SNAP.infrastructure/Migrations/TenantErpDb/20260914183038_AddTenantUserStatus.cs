using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNAP.infrastructure.Migrations.TenantErpDb
{
    /// <inheritdoc />
    public partial class AddTenantUserStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "TenantUsers");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "TenantUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // RolePermissionId 4/8/12 already hold Module=6 (the value 3/7/11 are moving into), so they
            // must be bumped to 7 first — otherwise the 3/7/11 updates momentarily collide with them
            // under the IX_RolePermissions_Role_Module unique index.
            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4,
                column: "Module",
                value: 7);

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8,
                column: "Module",
                value: 7);

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 12,
                column: "Module",
                value: 7);

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 3,
                column: "Module",
                value: 6);

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 7,
                column: "Module",
                value: 6);

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 11,
                column: "Module",
                value: 6);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "TenantUsers");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "TenantUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 3,
                column: "Module",
                value: 5);

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4,
                column: "Module",
                value: 6);

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 7,
                column: "Module",
                value: 5);

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8,
                column: "Module",
                value: 6);

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 11,
                column: "Module",
                value: 5);

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 12,
                column: "Module",
                value: 6);
        }
    }
}
