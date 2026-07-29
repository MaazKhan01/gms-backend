using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestOrganizationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "Guests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guests_OrganizationId",
                table: "Guests",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Guests_Organizations_OrganizationId",
                table: "Guests",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Guests_Organizations_OrganizationId",
                table: "Guests");

            migrationBuilder.DropIndex(
                name: "IX_Guests_OrganizationId",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Guests");
        }
    }
}
