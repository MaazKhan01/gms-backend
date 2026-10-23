using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class DelegateAppPrototypeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KeyOutcomes",
                table: "PostMissionReports",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "MissionJournalEntries",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "shared");

            migrationBuilder.AddColumn<string>(
                name: "Airline",
                table: "Flights",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ETicketNumber",
                table: "Flights",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pnr",
                table: "Flights",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaggageAllowance",
                table: "FlightLegs",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BoardingTime",
                table: "FlightLegs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Events",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Events",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoordinatorUserId",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccreditationCategory",
                table: "EventGuests",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccreditationCategoryLabel",
                table: "EventGuests",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccreditationNumber",
                table: "EventGuests",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccreditationZonesJson",
                table: "EventGuests",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PerDiemAmount",
                table: "EventGuests",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PerDiemCurrency",
                table: "EventGuests",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PerDiemStatus",
                table: "EventGuests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SettlementAmount",
                table: "EventGuests",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SettlementPaidOn",
                table: "EventGuests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoardBasis",
                table: "Accommodations",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "CheckInTime",
                table: "Accommodations",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "CheckOutTime",
                table: "Accommodations",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmationNumber",
                table: "Accommodations",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoomNumber",
                table: "Accommodations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookingChangeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventGuestId = table.Column<int>(type: "int", nullable: false),
                    BookingKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FlightId = table.Column<int>(type: "int", nullable: true),
                    AccommodationId = table.Column<int>(type: "int", nullable: true),
                    ChangeType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FromAirportId = table.Column<int>(type: "int", nullable: true),
                    ToAirportId = table.Column<int>(type: "int", nullable: true),
                    HotelId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "requested"),
                    ResolutionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResolvedBy = table.Column<int>(type: "int", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_BookingChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingChangeRequests_AccommodationHotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "AccommodationHotels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BookingChangeRequests_Accommodations_AccommodationId",
                        column: x => x.AccommodationId,
                        principalTable: "Accommodations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BookingChangeRequests_AirportData_FromAirportId",
                        column: x => x.FromAirportId,
                        principalTable: "AirportData",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BookingChangeRequests_AirportData_ToAirportId",
                        column: x => x.ToAirportId,
                        principalTable: "AirportData",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BookingChangeRequests_EventGuests_EventGuestId",
                        column: x => x.EventGuestId,
                        principalTable: "EventGuests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingChangeRequests_Flights_FlightId",
                        column: x => x.FlightId,
                        principalTable: "Flights",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BookingChangeRequests_Users_ResolvedBy",
                        column: x => x.ResolvedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_CoordinatorUserId",
                table: "Events",
                column: "CoordinatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingChangeRequests_AccommodationId",
                table: "BookingChangeRequests",
                column: "AccommodationId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingChangeRequests_EventGuestId_Status",
                table: "BookingChangeRequests",
                columns: new[] { "EventGuestId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingChangeRequests_FlightId",
                table: "BookingChangeRequests",
                column: "FlightId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingChangeRequests_FromAirportId",
                table: "BookingChangeRequests",
                column: "FromAirportId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingChangeRequests_HotelId",
                table: "BookingChangeRequests",
                column: "HotelId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingChangeRequests_PublicId",
                table: "BookingChangeRequests",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingChangeRequests_ResolvedBy",
                table: "BookingChangeRequests",
                column: "ResolvedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BookingChangeRequests_ToAirportId",
                table: "BookingChangeRequests",
                column: "ToAirportId");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Users_CoordinatorUserId",
                table: "Events",
                column: "CoordinatorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_Users_CoordinatorUserId",
                table: "Events");

            migrationBuilder.DropTable(
                name: "BookingChangeRequests");

            migrationBuilder.DropIndex(
                name: "IX_Events_CoordinatorUserId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "KeyOutcomes",
                table: "PostMissionReports");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "MissionJournalEntries");

            migrationBuilder.DropColumn(
                name: "Airline",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "ETicketNumber",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "Pnr",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "BaggageAllowance",
                table: "FlightLegs");

            migrationBuilder.DropColumn(
                name: "BoardingTime",
                table: "FlightLegs");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "CoordinatorUserId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "AccreditationCategory",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "AccreditationCategoryLabel",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "AccreditationNumber",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "AccreditationZonesJson",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "PerDiemAmount",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "PerDiemCurrency",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "PerDiemStatus",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "SettlementAmount",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "SettlementPaidOn",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "BoardBasis",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "CheckInTime",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "CheckOutTime",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "ConfirmationNumber",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "RoomNumber",
                table: "Accommodations");
        }
    }
}
