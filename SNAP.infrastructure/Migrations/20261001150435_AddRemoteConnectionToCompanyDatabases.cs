using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNAP.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRemoteConnectionToCompanyDatabases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RemoteDatabaseName",
                table: "CompanyDatabases",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemotePassword",
                table: "CompanyDatabases",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemoteServerName",
                table: "CompanyDatabases",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemoteUsername",
                table: "CompanyDatabases",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RemoteDatabaseName",
                table: "CompanyDatabases");

            migrationBuilder.DropColumn(
                name: "RemotePassword",
                table: "CompanyDatabases");

            migrationBuilder.DropColumn(
                name: "RemoteServerName",
                table: "CompanyDatabases");

            migrationBuilder.DropColumn(
                name: "RemoteUsername",
                table: "CompanyDatabases");
        }
    }
}
