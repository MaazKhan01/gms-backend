using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class addedimagecolumninhoteltable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Flights_FlightTypes_FlightTypeId",
                table: "Flights");

            migrationBuilder.DropTable(
                name: "FlightTypes");

            migrationBuilder.DropIndex(
                name: "IX_Flights_FlightTypeId",
                table: "Flights");

            migrationBuilder.RenameColumn(
                name: "FlightTypeId",
                table: "Flights",
                newName: "FlightType");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "AccommodationHotels",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "AccommodationHotels");

            migrationBuilder.RenameColumn(
                name: "FlightType",
                table: "Flights",
                newName: "FlightTypeId");

            migrationBuilder.CreateTable(
                name: "FlightTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValueSql: "((0))"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Flights_FlightTypeId",
                table: "Flights",
                column: "FlightTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightTypes_PublicId",
                table: "FlightTypes",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Flights_FlightTypes_FlightTypeId",
                table: "Flights",
                column: "FlightTypeId",
                principalTable: "FlightTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
