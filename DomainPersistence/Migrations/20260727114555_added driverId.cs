using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class addeddriverId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
                name: "Plate",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "VehiclePlate",
                table: "DriverProfiles");

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

            migrationBuilder.AddColumn<int>(
                name: "DriverId",
                table: "Transports",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transports_DriverId",
                table: "Transports",
                column: "DriverId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transports_DriverProfiles_DriverId",
                table: "Transports",
                column: "DriverId",
                principalTable: "DriverProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transports_DriverProfiles_DriverId",
                table: "Transports");

            migrationBuilder.DropIndex(
                name: "IX_Transports_DriverId",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "DriverId",
                table: "Transports");

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

            migrationBuilder.AddColumn<string>(
                name: "Plate",
                table: "Transports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehiclePlate",
                table: "DriverProfiles",
                type: "nvarchar(20)",
                maxLength: 20,
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
        }
    }
}
