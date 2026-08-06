using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class ServiceLevelsV2GlobalDynamic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // v1 catalogue data is dropped, as agreed: it was per-event test data
            // and none of it is meaningful once Services and Service Levels are
            // global. This has to run FIRST — the columns added below are NOT NULL
            // with a shared default, which would immediately violate the new
            // unique Code indexes if any rows survived.
            //
            // Hard delete rather than soft: these rows have no audit value and
            // leaving them would keep dead weight behind a filtered index forever.
            // Guest.Tier is deliberately untouched; it stays the legacy display
            // string that chips, CSV export and invitation targeting still read.
            migrationBuilder.Sql(@"
                UPDATE [Guests] SET [ServiceLevelId] = NULL WHERE [ServiceLevelId] IS NOT NULL;
                DELETE FROM [ServiceLevelServices];
                DELETE FROM [ServiceLevels];
                DELETE FROM [Services];");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceLevels_Events_EventId",
                table: "ServiceLevels");

            migrationBuilder.DropForeignKey(
                name: "FK_Services_Events_EventId",
                table: "Services");

            migrationBuilder.DropIndex(
                name: "IX_Services_EventId",
                table: "Services");

            migrationBuilder.DropIndex(
                name: "IX_ServiceLevels_EventId",
                table: "ServiceLevels");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "FieldValuesJson",
                table: "ServiceLevelServices");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "ServiceLevels");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "ServiceLevels");

            migrationBuilder.RenameColumn(
                name: "FieldsSchema",
                table: "Services",
                newName: "FormSchemaJson");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Services",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "Services",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Services",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "ServiceLevelServices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "ServiceLevels",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ServiceLevels",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "GuestServiceEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GuestId = table.Column<int>(type: "int", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedBy = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_GuestServiceEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestServiceEntries_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuestServiceEntries_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Services_Code",
                table: "Services",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLevels_Code",
                table: "ServiceLevels",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_GuestServiceEntries_GuestId_ServiceId",
                table: "GuestServiceEntries",
                columns: new[] { "GuestId", "ServiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestServiceEntries_PublicId",
                table: "GuestServiceEntries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestServiceEntries_ServiceId",
                table: "GuestServiceEntries",
                column: "ServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuestServiceEntries");

            migrationBuilder.DropIndex(
                name: "IX_Services_Code",
                table: "Services");

            migrationBuilder.DropIndex(
                name: "IX_ServiceLevels_Code",
                table: "ServiceLevels");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "ServiceLevelServices");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ServiceLevels");

            migrationBuilder.RenameColumn(
                name: "FormSchemaJson",
                table: "Services",
                newName: "FieldsSchema");

            migrationBuilder.AddColumn<int>(
                name: "EventId",
                table: "Services",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FieldValuesJson",
                table: "ServiceLevelServices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "ServiceLevels",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(60)",
                oldMaxLength: 60);

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "ServiceLevels",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EventId",
                table: "ServiceLevels",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Services_EventId",
                table: "Services",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLevels_EventId",
                table: "ServiceLevels",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceLevels_Events_EventId",
                table: "ServiceLevels",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Services_Events_EventId",
                table: "Services",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
