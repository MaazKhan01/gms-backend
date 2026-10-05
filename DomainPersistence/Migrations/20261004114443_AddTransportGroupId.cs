using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransportGroupId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TransportGroupId",
                table: "Transports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transports_TransportGroupId",
                table: "Transports",
                column: "TransportGroupId",
                filter: "[TransportGroupId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transports_TransportGroupId",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "TransportGroupId",
                table: "Transports");
        }
    }
}
