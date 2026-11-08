using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNAP.infrastructure.Migrations.TenantErpDb
{
    /// <inheritdoc />
    public partial class AddSalaryPaymentPaidTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                table: "SalaryPayments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidDate",
                table: "SalaryPayments",
                type: "datetime2",
                nullable: true);

            // Every existing payslip predates the two-step flow, when recording a salary payment
            // and paying it were the same action - so each one represents money already handed
            // over, on its PaymentDate. Without this they'd all read as unpaid.
            migrationBuilder.Sql(
                "UPDATE SalaryPayments SET IsPaid = 1, PaidDate = PaymentDate WHERE IsDeleted = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPaid",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "PaidDate",
                table: "SalaryPayments");
        }
    }
}
