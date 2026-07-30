using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeatPlaceholderAndRemovedSeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "RemovedSeats",
                table: "VenueLayoutProps",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Placeholder",
                table: "SeatProperties",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RemovedSeats",
                table: "VenueLayoutProps");

            migrationBuilder.DropColumn(
                name: "Placeholder",
                table: "SeatProperties");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "([IsDeleted] IS NULL OR [IsDeleted] = 0)");
        }
    }
}
