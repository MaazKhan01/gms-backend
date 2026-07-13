using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVenueBoxSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "VenueBoxes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VenueBoxes_SessionId",
                table: "VenueBoxes",
                column: "SessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_VenueBoxes_Sessions_SessionId",
                table: "VenueBoxes",
                column: "SessionId",
                principalTable: "Sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VenueBoxes_Sessions_SessionId",
                table: "VenueBoxes");

            migrationBuilder.DropIndex(
                name: "IX_VenueBoxes_SessionId",
                table: "VenueBoxes");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "VenueBoxes");
        }
    }
}
