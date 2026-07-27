using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class fixedcolumninairportDatatable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AirportName",
                table: "AirportData");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "AirportData",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Continent",
                table: "AirportData",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "AirportData",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "AirportData");

            migrationBuilder.DropColumn(
                name: "Continent",
                table: "AirportData");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "AirportData");

            migrationBuilder.AddColumn<string>(
                name: "AirportName",
                table: "AirportData",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
