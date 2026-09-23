using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNAP.infrastructure.Migrations.TenantErpDb
{
    /// <inheritdoc />
    public partial class AllowMultiplePaymentsPerBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_BookingId",
                table: "Payments");

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4,
                column: "AccessLevel",
                value: 4);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BookingId",
                table: "Payments",
                column: "BookingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_BookingId",
                table: "Payments");

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4,
                column: "AccessLevel",
                value: 2);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BookingId",
                table: "Payments",
                column: "BookingId",
                unique: true);
        }
    }
}
