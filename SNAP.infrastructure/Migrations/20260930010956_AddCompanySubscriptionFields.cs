using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNAP.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanySubscriptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionRenewalDate",
                table: "Companies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionStartDate",
                table: "Companies",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            // Existing companies started their subscription the day they were created, not on
            // 0001-01-01 (EF's default for a non-nullable DateTime) nor today.
            migrationBuilder.Sql("UPDATE Companies SET SubscriptionStartDate = CreatedAt;");

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionStatus",
                table: "Companies",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubscriptionRenewalDate",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "SubscriptionStartDate",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "SubscriptionStatus",
                table: "Companies");
        }
    }
}
