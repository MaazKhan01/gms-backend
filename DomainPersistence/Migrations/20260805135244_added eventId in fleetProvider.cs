using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class addedeventIdinfleetProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transports_VehicleId",
                table: "Transports");

            migrationBuilder.DropIndex(
                name: "IX_FleetProviders_Name",
                table: "FleetProviders");

            migrationBuilder.AddColumn<int>(
                name: "EventId",
                table: "FleetProviders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Transports_VehicleId_PickupTime",
                table: "Transports",
                columns: new[] { "VehicleId", "PickupTime" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetProviders_EventId_Name",
                table: "FleetProviders",
                columns: new[] { "EventId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_FleetProviders_Events_EventId",
                table: "FleetProviders",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FleetProviders_Events_EventId",
                table: "FleetProviders");

            migrationBuilder.DropIndex(
                name: "IX_Transports_VehicleId_PickupTime",
                table: "Transports");

            migrationBuilder.DropIndex(
                name: "IX_FleetProviders_EventId_Name",
                table: "FleetProviders");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "FleetProviders");

            migrationBuilder.CreateIndex(
                name: "IX_Transports_VehicleId",
                table: "Transports",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetProviders_Name",
                table: "FleetProviders",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
