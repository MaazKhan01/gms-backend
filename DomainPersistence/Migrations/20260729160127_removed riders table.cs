using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class removedriderstable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transports_RideRequests_RideRequestId",
                table: "Transports");

            migrationBuilder.DropTable(
                name: "RideRequests");

            migrationBuilder.DropIndex(
                name: "IX_Transports_RideRequestId",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "RideRequestId",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "AvailabilityChangedAt",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: "DriverProfiles");

            migrationBuilder.AlterColumn<bool>(
                name: "IsOnline",
                table: "DriverProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RideRequestId",
                table: "Transports",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsOnline",
                table: "DriverProfiles",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

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
                name: "RideRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AcceptedByDriverId = table.Column<int>(type: "int", nullable: true),
                    DropoffLocationId = table.Column<int>(type: "int", nullable: true),
                    GuestId = table.Column<int>(type: "int", nullable: false),
                    PickupLocationId = table.Column<int>(type: "int", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValueSql: "((0))"),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    RequestedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Open"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_Transports_RideRequestId",
                table: "Transports",
                column: "RideRequestId",
                unique: true,
                filter: "[RideRequestId] IS NOT NULL");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Transports_RideRequests_RideRequestId",
                table: "Transports",
                column: "RideRequestId",
                principalTable: "RideRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
