using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNAP.infrastructure.Migrations.TenantErpDb
{
    /// <inheritdoc />
    public partial class AddPayrollFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "SalaryPayments",
                newName: "WithholdingTax");

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlySalary",
                table: "TenantUsers",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossAmount",
                table: "SalaryPayments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetAmount",
                table: "SalaryPayments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PagIbigDeduction",
                table: "SalaryPayments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PhilHealthDeduction",
                table: "SalaryPayments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SssDeduction",
                table: "SalaryPayments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MonthlySalary",
                table: "TenantUsers");

            migrationBuilder.DropColumn(
                name: "GrossAmount",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "NetAmount",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "PagIbigDeduction",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "PhilHealthDeduction",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "SssDeduction",
                table: "SalaryPayments");

            migrationBuilder.RenameColumn(
                name: "WithholdingTax",
                table: "SalaryPayments",
                newName: "Amount");
        }
    }
}
