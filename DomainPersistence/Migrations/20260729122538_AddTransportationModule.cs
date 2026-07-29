using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransportationModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BaseFare",
                table: "Transports",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Transports",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DistanceFare",
                table: "Transports",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Transports",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RideRequestId",
                table: "Transports",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RideSource",
                table: "Transports",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalFare",
                table: "Transports",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WaitingFare",
                table: "Transports",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailabilityChangedAt",
                table: "DriverProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                table: "DriverProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "GuestDriverAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GuestId = table.Column<int>(type: "int", nullable: false),
                    DriverId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_GuestDriverAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestDriverAssignments_DriverProfiles_DriverId",
                        column: x => x.DriverId,
                        principalTable: "DriverProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuestDriverAssignments_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RideRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GuestId = table.Column<int>(type: "int", nullable: false),
                    PickupLocationId = table.Column<int>(type: "int", nullable: true),
                    DropoffLocationId = table.Column<int>(type: "int", nullable: true),
                    RequestedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Open"),
                    AcceptedByDriverId = table.Column<int>(type: "int", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
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
                    table.PrimaryKey("PK_RideRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RideRequests_DriverProfiles_AcceptedByDriverId",
                        column: x => x.AcceptedByDriverId,
                        principalTable: "DriverProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RideRequests_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RideRequests_Locations_DropoffLocationId",
                        column: x => x.DropoffLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RideRequests_Locations_PickupLocationId",
                        column: x => x.PickupLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransportStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransportId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_TransportStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransportStatusHistories_Transports_TransportId",
                        column: x => x.TransportId,
                        principalTable: "Transports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transports_RideRequestId",
                table: "Transports",
                column: "RideRequestId",
                unique: true,
                filter: "[RideRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GuestDriverAssignments_DriverId",
                table: "GuestDriverAssignments",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestDriverAssignments_GuestId_DriverId",
                table: "GuestDriverAssignments",
                columns: new[] { "GuestId", "DriverId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_GuestDriverAssignments_PublicId",
                table: "GuestDriverAssignments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RideRequests_AcceptedByDriverId",
                table: "RideRequests",
                column: "AcceptedByDriverId");

            migrationBuilder.CreateIndex(
                name: "IX_RideRequests_DropoffLocationId",
                table: "RideRequests",
                column: "DropoffLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_RideRequests_GuestId_Status",
                table: "RideRequests",
                columns: new[] { "GuestId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RideRequests_PickupLocationId",
                table: "RideRequests",
                column: "PickupLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_RideRequests_PublicId",
                table: "RideRequests",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransportStatusHistories_PublicId",
                table: "TransportStatusHistories",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransportStatusHistories_TransportId_CreatedAt",
                table: "TransportStatusHistories",
                columns: new[] { "TransportId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Transports_RideRequests_RideRequestId",
                table: "Transports",
                column: "RideRequestId",
                principalTable: "RideRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transports_RideRequests_RideRequestId",
                table: "Transports");

            migrationBuilder.DropTable(
                name: "GuestDriverAssignments");

            migrationBuilder.DropTable(
                name: "RideRequests");

            migrationBuilder.DropTable(
                name: "TransportStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_Transports_RideRequestId",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "BaseFare",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "DistanceFare",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "RideRequestId",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "RideSource",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "TotalFare",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "WaitingFare",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "AvailabilityChangedAt",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: "DriverProfiles");
        }
    }
}
