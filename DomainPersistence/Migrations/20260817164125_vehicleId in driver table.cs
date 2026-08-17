using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class vehicleIdindrivertable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UsageType",
                table: "Vehicles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedVehicleId",
                table: "DriverProfiles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DriverProfiles_AssignedVehicleId",
                table: "DriverProfiles",
                column: "AssignedVehicleId",
                unique: true,
                filter: "[AssignedVehicleId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_DriverProfiles_Vehicles_AssignedVehicleId",
                table: "DriverProfiles",
                column: "AssignedVehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DriverProfiles_Vehicles_AssignedVehicleId",
                table: "DriverProfiles");

            migrationBuilder.DropIndex(
                name: "IX_DriverProfiles_AssignedVehicleId",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "UsageType",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "AssignedVehicleId",
                table: "DriverProfiles");
        }
    }
}
