using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNAP.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyPlanTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlanTier",
                table: "Companies",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlanTier",
                table: "Companies");
        }
    }
}
