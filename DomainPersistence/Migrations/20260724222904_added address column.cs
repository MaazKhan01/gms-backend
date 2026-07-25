using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class addedaddresscolumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivalDate",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "DepartureDate",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "SeatId",
                table: "Guests");

            migrationBuilder.AddColumn<string>(
                name: "DriverName",
                table: "Transports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriverPhone",
                table: "Transports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DriverRating",
                table: "Transports",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EstimatedArrival",
                table: "Transports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PickupTime",
                table: "Transports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Plate",
                table: "Transports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TripStatus",
                table: "Transports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleType",
                table: "Transports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Seat",
                table: "Flights",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Flights",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartTime",
                table: "FlightLegs",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndTime",
                table: "FlightLegs",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

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

            migrationBuilder.AddColumn<string>(
                name: "ConciergeName",
                table: "Accommodations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConciergePhone",
                table: "Accommodations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GuestCount",
                table: "Accommodations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoomView",
                table: "Accommodations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "AccommodationHotels",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DriverName",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "DriverPhone",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "DriverRating",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "EstimatedArrival",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "PickupTime",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "Plate",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "TripStatus",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "VehicleType",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "Seat",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Flights");

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

            migrationBuilder.DropColumn(
                name: "ConciergeName",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "ConciergePhone",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "GuestCount",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "RoomView",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "AccommodationHotels");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ArrivalDate",
                table: "Guests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DepartureDate",
                table: "Guests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeatId",
                table: "Guests",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartTime",
                table: "FlightLegs",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndTime",
                table: "FlightLegs",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
