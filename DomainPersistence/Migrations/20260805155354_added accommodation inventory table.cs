using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class addedaccommodationinventorytable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventHotelContracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    AccommodationHotelId = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValueSql: "((0))"),
                    DeletedBy = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventHotelContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventHotelContracts_AccommodationHotels_AccommodationHotelId",
                        column: x => x.AccommodationHotelId,
                        principalTable: "AccommodationHotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventHotelContracts_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HotelRoomInventories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventHotelContractId = table.Column<int>(type: "int", nullable: false),
                    RoomTypeId = table.Column<int>(type: "int", nullable: false),
                    RoomCount = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValueSql: "((0))"),
                    DeletedBy = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HotelRoomInventories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HotelRoomInventories_AccommodationRoomTypes_RoomTypeId",
                        column: x => x.RoomTypeId,
                        principalTable: "AccommodationRoomTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HotelRoomInventories_EventHotelContracts_EventHotelContractId",
                        column: x => x.EventHotelContractId,
                        principalTable: "EventHotelContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventHotelContracts_AccommodationHotelId",
                table: "EventHotelContracts",
                column: "AccommodationHotelId");

            migrationBuilder.CreateIndex(
                name: "IX_EventHotelContracts_EventId_AccommodationHotelId",
                table: "EventHotelContracts",
                columns: new[] { "EventId", "AccommodationHotelId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EventHotelContracts_PublicId",
                table: "EventHotelContracts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HotelRoomInventories_EventHotelContractId_RoomTypeId_FromDate",
                table: "HotelRoomInventories",
                columns: new[] { "EventHotelContractId", "RoomTypeId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HotelRoomInventories_PublicId",
                table: "HotelRoomInventories",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HotelRoomInventories_RoomTypeId",
                table: "HotelRoomInventories",
                column: "RoomTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HotelRoomInventories");

            migrationBuilder.DropTable(
                name: "EventHotelContracts");
        }
    }
}
