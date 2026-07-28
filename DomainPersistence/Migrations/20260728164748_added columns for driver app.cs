using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class addedcolumnsfordriverapp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DriverProfiles_VehicleTypes_VehicleTypeId",
                table: "DriverProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Transports_VehicleTypes_VehicleTypeId",
                table: "Transports");

            migrationBuilder.DropIndex(
                name: "IX_DriverProfiles_VehicleTypeId",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "Age",
                table: "DriverProfiles");

            migrationBuilder.RenameColumn(
                name: "VehicleTypeId",
                table: "Transports",
                newName: "VehicleId");

            migrationBuilder.RenameColumn(
                name: "EstimatedArrival",
                table: "Transports",
                newName: "DropoffTime");

            migrationBuilder.RenameIndex(
                name: "IX_Transports_VehicleTypeId",
                table: "Transports",
                newName: "IX_Transports_VehicleId");

            migrationBuilder.RenameColumn(
                name: "VehicleTypeId",
                table: "DriverProfiles",
                newName: "DriverType");

            migrationBuilder.AddColumn<DateTime>(
                name: "ActualDropOffTime",
                table: "Transports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActualPickupTime",
                table: "Transports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PortalAccess",
                table: "Roles",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleTypeId = table.Column<int>(type: "int", nullable: false),
                    VehicleModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VehicleNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VehicleImage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehicles_VehicleTypes_VehicleTypeId",
                        column: x => x.VehicleTypeId,
                        principalTable: "VehicleTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_PublicId",
                table: "Vehicles",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_VehicleTypeId",
                table: "Vehicles",
                column: "VehicleTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transports_Vehicles_VehicleId",
                table: "Transports",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transports_Vehicles_VehicleId",
                table: "Transports");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ActualDropOffTime",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "ActualPickupTime",
                table: "Transports");

            migrationBuilder.DropColumn(
                name: "PortalAccess",
                table: "Roles");

            migrationBuilder.RenameColumn(
                name: "VehicleId",
                table: "Transports",
                newName: "VehicleTypeId");

            migrationBuilder.RenameColumn(
                name: "DropoffTime",
                table: "Transports",
                newName: "EstimatedArrival");

            migrationBuilder.RenameIndex(
                name: "IX_Transports_VehicleId",
                table: "Transports",
                newName: "IX_Transports_VehicleTypeId");

            migrationBuilder.RenameColumn(
                name: "DriverType",
                table: "DriverProfiles",
                newName: "VehicleTypeId");

            migrationBuilder.AddColumn<int>(
                name: "Age",
                table: "DriverProfiles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DriverProfiles_VehicleTypeId",
                table: "DriverProfiles",
                column: "VehicleTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_DriverProfiles_VehicleTypes_VehicleTypeId",
                table: "DriverProfiles",
                column: "VehicleTypeId",
                principalTable: "VehicleTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transports_VehicleTypes_VehicleTypeId",
                table: "Transports",
                column: "VehicleTypeId",
                principalTable: "VehicleTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
