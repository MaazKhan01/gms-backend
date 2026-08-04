using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightLegClassAndSeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FlightClassId",
                table: "FlightLegs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Seat",
                table: "FlightLegs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlightLegs_FlightClassId",
                table: "FlightLegs",
                column: "FlightClassId");

            migrationBuilder.AddForeignKey(
                name: "FK_FlightLegs_FlightClasses_FlightClassId",
                table: "FlightLegs",
                column: "FlightClassId",
                principalTable: "FlightClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FlightLegs_FlightClasses_FlightClassId",
                table: "FlightLegs");

            migrationBuilder.DropIndex(
                name: "IX_FlightLegs_FlightClassId",
                table: "FlightLegs");

            migrationBuilder.DropColumn(
                name: "FlightClassId",
                table: "FlightLegs");

            migrationBuilder.DropColumn(
                name: "Seat",
                table: "FlightLegs");
        }
    }
}
