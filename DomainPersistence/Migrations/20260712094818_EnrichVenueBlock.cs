using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class EnrichVenueBlock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "VenueLayoutId",
                table: "VenueLayoutProps",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "VenueBlockId",
                table: "VenueLayoutProps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Rotation",
                table: "VenueBlocks",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "VenueBlocks",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VenueLayoutProps_VenueBlockId",
                table: "VenueLayoutProps",
                column: "VenueBlockId");

            migrationBuilder.AddForeignKey(
                name: "FK_VenueLayoutProps_VenueBlocks_VenueBlockId",
                table: "VenueLayoutProps",
                column: "VenueBlockId",
                principalTable: "VenueBlocks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VenueLayoutProps_VenueBlocks_VenueBlockId",
                table: "VenueLayoutProps");

            migrationBuilder.DropIndex(
                name: "IX_VenueLayoutProps_VenueBlockId",
                table: "VenueLayoutProps");

            migrationBuilder.DropColumn(
                name: "VenueBlockId",
                table: "VenueLayoutProps");

            migrationBuilder.DropColumn(
                name: "Rotation",
                table: "VenueBlocks");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "VenueBlocks");

            migrationBuilder.AlterColumn<Guid>(
                name: "VenueLayoutId",
                table: "VenueLayoutProps",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
