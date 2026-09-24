using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class HostOrganizationLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HostOrganizationId",
                table: "HostInvitations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HostOrganizationId",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HostInvitations_HostOrganizationId",
                table: "HostInvitations",
                column: "HostOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_HostOrganizationId",
                table: "Events",
                column: "HostOrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Organizations_HostOrganizationId",
                table: "Events",
                column: "HostOrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HostInvitations_Organizations_HostOrganizationId",
                table: "HostInvitations",
                column: "HostOrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_Organizations_HostOrganizationId",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_HostInvitations_Organizations_HostOrganizationId",
                table: "HostInvitations");

            migrationBuilder.DropIndex(
                name: "IX_HostInvitations_HostOrganizationId",
                table: "HostInvitations");

            migrationBuilder.DropIndex(
                name: "IX_Events_HostOrganizationId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "HostOrganizationId",
                table: "HostInvitations");

            migrationBuilder.DropColumn(
                name: "HostOrganizationId",
                table: "Events");
        }
    }
}
