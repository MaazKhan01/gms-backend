using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class addedairportDatatable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivalCity",
                table: "FlightLegs");

            migrationBuilder.DropColumn(
                name: "ArrivalCode",
                table: "FlightLegs");

            migrationBuilder.DropColumn(
                name: "DepartureCity",
                table: "FlightLegs");

            migrationBuilder.DropColumn(
                name: "DepartureCode",
                table: "FlightLegs");

            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "Venues",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Locations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FromAirportId",
                table: "FlightLegs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToAirportId",
                table: "FlightLegs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "AccommodationHotels",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AirportData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AirportName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValueSql: "((0))"),
                    DeletedBy = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirportData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AirportData_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Venues_LocationId",
                table: "Venues",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightLegs_FromAirportId",
                table: "FlightLegs",
                column: "FromAirportId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightLegs_ToAirportId",
                table: "FlightLegs",
                column: "ToAirportId");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationHotels_LocationId",
                table: "AccommodationHotels",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_AirportData_Code",
                table: "AirportData",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AirportData_LocationId",
                table: "AirportData",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_AirportData_PublicId",
                table: "AirportData",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AccommodationHotels_Locations_LocationId",
                table: "AccommodationHotels",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FlightLegs_AirportData_FromAirportId",
                table: "FlightLegs",
                column: "FromAirportId",
                principalTable: "AirportData",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FlightLegs_AirportData_ToAirportId",
                table: "FlightLegs",
                column: "ToAirportId",
                principalTable: "AirportData",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Venues_Locations_LocationId",
                table: "Venues",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccommodationHotels_Locations_LocationId",
                table: "AccommodationHotels");

            migrationBuilder.DropForeignKey(
                name: "FK_FlightLegs_AirportData_FromAirportId",
                table: "FlightLegs");

            migrationBuilder.DropForeignKey(
                name: "FK_FlightLegs_AirportData_ToAirportId",
                table: "FlightLegs");

            migrationBuilder.DropForeignKey(
                name: "FK_Venues_Locations_LocationId",
                table: "Venues");

            migrationBuilder.DropTable(
                name: "AirportData");

            migrationBuilder.DropIndex(
                name: "IX_Venues_LocationId",
                table: "Venues");

            migrationBuilder.DropIndex(
                name: "IX_FlightLegs_FromAirportId",
                table: "FlightLegs");

            migrationBuilder.DropIndex(
                name: "IX_FlightLegs_ToAirportId",
                table: "FlightLegs");

            migrationBuilder.DropIndex(
                name: "IX_AccommodationHotels_LocationId",
                table: "AccommodationHotels");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "FromAirportId",
                table: "FlightLegs");

            migrationBuilder.DropColumn(
                name: "ToAirportId",
                table: "FlightLegs");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "AccommodationHotels");

            migrationBuilder.AddColumn<string>(
                name: "ArrivalCity",
                table: "FlightLegs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArrivalCode",
                table: "FlightLegs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepartureCity",
                table: "FlightLegs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepartureCode",
                table: "FlightLegs",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
