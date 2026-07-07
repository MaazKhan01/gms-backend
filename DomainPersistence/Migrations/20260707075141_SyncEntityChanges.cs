using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncEntityChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Travel_logistics_GuestId",
                table: "Travel_logistics",
                column: "GuestId");

            migrationBuilder.AddForeignKey(
                name: "FK_Travel_logistics_Guests_GuestId",
                table: "Travel_logistics",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Travel_logistics_Guests_GuestId",
                table: "Travel_logistics");

            migrationBuilder.DropIndex(
                name: "IX_Travel_logistics_GuestId",
                table: "Travel_logistics");
        }
    }
}
