using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitGuestIntoPersonAndEventGuest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            // ════════════════════════════════════════════════════════════════════
            //  DATA MIGRATION — Guests(person+event) -> Guests(person) + EventGuests
            //
            //  EF scaffolds the DDL but not the data, and its default order drops the
            //  per-event columns off Guests before anything can copy them. So the
            //  values are stashed in a real table FIRST (not #temp: this must survive
            //  every batch in the migration), and replayed into EventGuests further
            //  down, after that table exists.
            //
            //  The child tables (Flights/Accommodations/Transports/Invitations/
            //  GuestSessions/GuestServiceEntries/SeatAssigns/GuestDriverAssignments)
            //  are only RENAMED GuestId -> EventGuestId, keeping their values. That is
            //  safe because EventGuests is populated with Id = the old Guests.Id, so
            //  every existing FK value still points at the right row.
            // ════════════════════════════════════════════════════════════════════
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.__GuestSplitStash', 'U') IS NOT NULL
                    DROP TABLE dbo.__GuestSplitStash;

                SELECT
                    [Id], [EventId], [GuestType], [Organization], [OrganizationId],
                    [ServiceLevelId], [ServiceLevelRulesOverridden],
                    [ServiceLevelOverrideReason], [AccreditationRequired],
                    [AllowedServicesJson], [CreatedAt], [CreatedBy], [IsDeleted]
                INTO dbo.__GuestSplitStash
                FROM [Guests];");

            // ── Email becomes the person's identity: required + unique ───────────
            // Pre-existing rows may have no email at all (it used to be optional).
            // A deterministic placeholder keeps the row and its bookings rather than
            // deleting a guest we can't identify; it is obviously invalid on sight.
            migrationBuilder.Sql(@"
                UPDATE [Guests]
                SET [Email] = CONCAT('guest-', [Id], '@no-email.invalid')
                WHERE [Email] IS NULL OR LTRIM(RTRIM([Email])) = '';");
            migrationBuilder.DropForeignKey(
                name: "FK_Accommodations_Guests_GuestId",
                table: "Accommodations");

            migrationBuilder.DropForeignKey(
                name: "FK_Flights_Guests_GuestId",
                table: "Flights");

            migrationBuilder.DropForeignKey(
                name: "FK_GuestDriverAssignments_Guests_GuestId",
                table: "GuestDriverAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_Guests_Events_EventId",
                table: "Guests");

            migrationBuilder.DropForeignKey(
                name: "FK_Guests_Organizations_OrganizationId",
                table: "Guests");

            migrationBuilder.DropForeignKey(
                name: "FK_Guests_ServiceLevels_ServiceLevelId",
                table: "Guests");

            migrationBuilder.DropForeignKey(
                name: "FK_GuestServiceEntries_Guests_GuestId",
                table: "GuestServiceEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_GuestSessions_Guests_GuestId",
                table: "GuestSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_Guests_GuestId",
                table: "Invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_SeatAssigns_Guests_GuestId",
                table: "SeatAssigns");

            migrationBuilder.DropForeignKey(
                name: "FK_Transports_Guests_GuestId",
                table: "Transports");

            migrationBuilder.DropIndex(
                name: "IX_Guests_EventId",
                table: "Guests");

            migrationBuilder.DropIndex(
                name: "IX_Guests_OrganizationId",
                table: "Guests");

            migrationBuilder.DropIndex(
                name: "IX_Guests_ServiceLevelId",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "AccreditationRequired",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "AllowedServicesJson",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "ArrivalDate",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "DepartureDate",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "GuestType",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "Organization",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "ServiceLevelId",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "ServiceLevelOverrideReason",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "ServiceLevelRulesOverridden",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "Tier",
                table: "Guests");

            migrationBuilder.RenameColumn(
                name: "GuestId",
                table: "Transports",
                newName: "EventGuestId");

            migrationBuilder.RenameIndex(
                name: "IX_Transports_GuestId",
                table: "Transports",
                newName: "IX_Transports_EventGuestId");

            migrationBuilder.RenameColumn(
                name: "GuestId",
                table: "SeatAssigns",
                newName: "EventGuestId");

            migrationBuilder.RenameIndex(
                name: "IX_SeatAssigns_GuestId",
                table: "SeatAssigns",
                newName: "IX_SeatAssigns_EventGuestId");

            migrationBuilder.RenameColumn(
                name: "GuestId",
                table: "Invitations",
                newName: "EventGuestId");

            migrationBuilder.RenameIndex(
                name: "IX_Invitations_GuestId",
                table: "Invitations",
                newName: "IX_Invitations_EventGuestId");

            migrationBuilder.RenameColumn(
                name: "GuestId",
                table: "GuestSessions",
                newName: "EventGuestId");

            migrationBuilder.RenameColumn(
                name: "GuestId",
                table: "GuestServiceEntries",
                newName: "EventGuestId");

            migrationBuilder.RenameIndex(
                name: "IX_GuestServiceEntries_GuestId_ServiceId",
                table: "GuestServiceEntries",
                newName: "IX_GuestServiceEntries_EventGuestId_ServiceId");

            migrationBuilder.RenameColumn(
                name: "GuestId",
                table: "GuestDriverAssignments",
                newName: "EventGuestId");

            migrationBuilder.RenameIndex(
                name: "IX_GuestDriverAssignments_GuestId_DriverId",
                table: "GuestDriverAssignments",
                newName: "IX_GuestDriverAssignments_EventGuestId_DriverId");

            migrationBuilder.RenameColumn(
                name: "GuestId",
                table: "Flights",
                newName: "EventGuestId");

            migrationBuilder.RenameIndex(
                name: "IX_Flights_GuestId",
                table: "Flights",
                newName: "IX_Flights_EventGuestId");

            migrationBuilder.RenameColumn(
                name: "GuestId",
                table: "Accommodations",
                newName: "EventGuestId");

            migrationBuilder.RenameIndex(
                name: "IX_Accommodations_GuestId",
                table: "Accommodations",
                newName: "IX_Accommodations_EventGuestId");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Guests",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "EventGuests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GuestId = table.Column<int>(type: "int", nullable: false),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    GuestType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Organization = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    OrganizationId = table.Column<int>(type: "int", nullable: true),
                    ServiceLevelId = table.Column<int>(type: "int", nullable: true),
                    ServiceLevelRulesOverridden = table.Column<bool>(type: "bit", nullable: false),
                    ServiceLevelOverrideReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccreditationRequired = table.Column<bool>(type: "bit", nullable: false),
                    AllowedServicesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_EventGuests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventGuests_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventGuests_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventGuests_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EventGuests_ServiceLevels_ServiceLevelId",
                        column: x => x.ServiceLevelId,
                        principalTable: "ServiceLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });


            // ── replay the stashed per-event data into EventGuests ───────────────
            // IDENTITY_INSERT so EventGuests.Id == the old Guests.Id, which is what
            // makes the child-table renames above correct with no value remapping.
            migrationBuilder.Sql(@"
                SET IDENTITY_INSERT [EventGuests] ON;

                INSERT INTO [EventGuests]
                    ([Id], [GuestId], [EventId], [GuestType], [Organization],
                     [OrganizationId], [ServiceLevelId], [ServiceLevelRulesOverridden],
                     [ServiceLevelOverrideReason], [AccreditationRequired],
                     [AllowedServicesJson], [CreatedAt], [CreatedBy], [IsDeleted])
                SELECT
                    st.[Id],
                    st.[Id],            -- GuestId: still 1:1 here; deduped just below
                    st.[EventId],
                    st.[GuestType],
                    st.[Organization],
                    st.[OrganizationId],
                    st.[ServiceLevelId],
                    ISNULL(st.[ServiceLevelRulesOverridden], 0),
                    st.[ServiceLevelOverrideReason],
                    ISNULL(st.[AccreditationRequired], 0),
                    st.[AllowedServicesJson],
                    ISNULL(st.[CreatedAt], SYSUTCDATETIME()),
                    st.[CreatedBy],
                    ISNULL(st.[IsDeleted], 0)
                FROM dbo.__GuestSplitStash st
                INNER JOIN [Guests] g ON g.[Id] = st.[Id];

                SET IDENTITY_INSERT [EventGuests] OFF;");

            // ── collapse duplicate PEOPLE ────────────────────────────────────────
            // Until now the same human got one Guests row per event, so an email can
            // appear many times. Keep the lowest-Id row as the person, repoint every
            // participation at it, and SOFT-delete the rest.
            //
            // Soft, not hard: those rows own a 1:1 User (plus refresh tokens, support
            // conversations), and hard-deleting would cascade into them. The unique
            // index below is filtered on [IsDeleted] = 0 precisely so the retired
            // duplicates can sit there harmlessly.
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.__GuestDedupe', 'U') IS NOT NULL
                    DROP TABLE dbo.__GuestDedupe;

                SELECT
                    [Id],
                    LOWER(LTRIM(RTRIM([Email]))) AS [Key],
                    ROW_NUMBER() OVER (
                        PARTITION BY LOWER(LTRIM(RTRIM([Email])))
                        ORDER BY [Id]) AS [Rn]
                INTO dbo.__GuestDedupe
                FROM [Guests]
                WHERE [IsDeleted] = 0 OR [IsDeleted] IS NULL;

                UPDATE eg
                SET eg.[GuestId] = keep.[Id]
                FROM [EventGuests] eg
                INNER JOIN dbo.__GuestDedupe dup ON dup.[Id] = eg.[GuestId] AND dup.[Rn] > 1
                INNER JOIN dbo.__GuestDedupe keep ON keep.[Key] = dup.[Key] AND keep.[Rn] = 1;

                UPDATE g
                SET g.[IsDeleted] = 1, g.[DeletedAt] = SYSUTCDATETIME()
                FROM [Guests] g
                INNER JOIN dbo.__GuestDedupe dup ON dup.[Id] = g.[Id]
                WHERE dup.[Rn] > 1;

                DROP TABLE dbo.__GuestDedupe;
                DROP TABLE dbo.__GuestSplitStash;");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_Email",
                table: "Guests",
                column: "Email",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EventGuests_EventId",
                table: "EventGuests",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventGuests_GuestId_EventId",
                table: "EventGuests",
                columns: new[] { "GuestId", "EventId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EventGuests_OrganizationId",
                table: "EventGuests",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_EventGuests_PublicId",
                table: "EventGuests",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventGuests_ServiceLevelId",
                table: "EventGuests",
                column: "ServiceLevelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Accommodations_EventGuests_EventGuestId",
                table: "Accommodations",
                column: "EventGuestId",
                principalTable: "EventGuests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Flights_EventGuests_EventGuestId",
                table: "Flights",
                column: "EventGuestId",
                principalTable: "EventGuests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GuestDriverAssignments_EventGuests_EventGuestId",
                table: "GuestDriverAssignments",
                column: "EventGuestId",
                principalTable: "EventGuests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GuestServiceEntries_EventGuests_EventGuestId",
                table: "GuestServiceEntries",
                column: "EventGuestId",
                principalTable: "EventGuests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GuestSessions_EventGuests_EventGuestId",
                table: "GuestSessions",
                column: "EventGuestId",
                principalTable: "EventGuests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_EventGuests_EventGuestId",
                table: "Invitations",
                column: "EventGuestId",
                principalTable: "EventGuests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SeatAssigns_EventGuests_EventGuestId",
                table: "SeatAssigns",
                column: "EventGuestId",
                principalTable: "EventGuests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transports_EventGuests_EventGuestId",
                table: "Transports",
                column: "EventGuestId",
                principalTable: "EventGuests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accommodations_EventGuests_EventGuestId",
                table: "Accommodations");

            migrationBuilder.DropForeignKey(
                name: "FK_Flights_EventGuests_EventGuestId",
                table: "Flights");

            migrationBuilder.DropForeignKey(
                name: "FK_GuestDriverAssignments_EventGuests_EventGuestId",
                table: "GuestDriverAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_GuestServiceEntries_EventGuests_EventGuestId",
                table: "GuestServiceEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_GuestSessions_EventGuests_EventGuestId",
                table: "GuestSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_EventGuests_EventGuestId",
                table: "Invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_SeatAssigns_EventGuests_EventGuestId",
                table: "SeatAssigns");

            migrationBuilder.DropForeignKey(
                name: "FK_Transports_EventGuests_EventGuestId",
                table: "Transports");

            migrationBuilder.DropTable(
                name: "EventGuests");

            migrationBuilder.DropIndex(
                name: "IX_Guests_Email",
                table: "Guests");

            migrationBuilder.RenameColumn(
                name: "EventGuestId",
                table: "Transports",
                newName: "GuestId");

            migrationBuilder.RenameIndex(
                name: "IX_Transports_EventGuestId",
                table: "Transports",
                newName: "IX_Transports_GuestId");

            migrationBuilder.RenameColumn(
                name: "EventGuestId",
                table: "SeatAssigns",
                newName: "GuestId");

            migrationBuilder.RenameIndex(
                name: "IX_SeatAssigns_EventGuestId",
                table: "SeatAssigns",
                newName: "IX_SeatAssigns_GuestId");

            migrationBuilder.RenameColumn(
                name: "EventGuestId",
                table: "Invitations",
                newName: "GuestId");

            migrationBuilder.RenameIndex(
                name: "IX_Invitations_EventGuestId",
                table: "Invitations",
                newName: "IX_Invitations_GuestId");

            migrationBuilder.RenameColumn(
                name: "EventGuestId",
                table: "GuestSessions",
                newName: "GuestId");

            migrationBuilder.RenameColumn(
                name: "EventGuestId",
                table: "GuestServiceEntries",
                newName: "GuestId");

            migrationBuilder.RenameIndex(
                name: "IX_GuestServiceEntries_EventGuestId_ServiceId",
                table: "GuestServiceEntries",
                newName: "IX_GuestServiceEntries_GuestId_ServiceId");

            migrationBuilder.RenameColumn(
                name: "EventGuestId",
                table: "GuestDriverAssignments",
                newName: "GuestId");

            migrationBuilder.RenameIndex(
                name: "IX_GuestDriverAssignments_EventGuestId_DriverId",
                table: "GuestDriverAssignments",
                newName: "IX_GuestDriverAssignments_GuestId_DriverId");

            migrationBuilder.RenameColumn(
                name: "EventGuestId",
                table: "Flights",
                newName: "GuestId");

            migrationBuilder.RenameIndex(
                name: "IX_Flights_EventGuestId",
                table: "Flights",
                newName: "IX_Flights_GuestId");

            migrationBuilder.RenameColumn(
                name: "EventGuestId",
                table: "Accommodations",
                newName: "GuestId");

            migrationBuilder.RenameIndex(
                name: "IX_Accommodations_EventGuestId",
                table: "Accommodations",
                newName: "IX_Accommodations_GuestId");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Guests",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<bool>(
                name: "AccreditationRequired",
                table: "Guests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AllowedServicesJson",
                table: "Guests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ArrivalDate",
                table: "Guests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DepartureDate",
                table: "Guests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EventId",
                table: "Guests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GuestType",
                table: "Guests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Organization",
                table: "Guests",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "Guests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceLevelId",
                table: "Guests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceLevelOverrideReason",
                table: "Guests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ServiceLevelRulesOverridden",
                table: "Guests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Tier",
                table: "Guests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guests_EventId",
                table: "Guests",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_OrganizationId",
                table: "Guests",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_ServiceLevelId",
                table: "Guests",
                column: "ServiceLevelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Accommodations_Guests_GuestId",
                table: "Accommodations",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Flights_Guests_GuestId",
                table: "Flights",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GuestDriverAssignments_Guests_GuestId",
                table: "GuestDriverAssignments",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Guests_Events_EventId",
                table: "Guests",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Guests_Organizations_OrganizationId",
                table: "Guests",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Guests_ServiceLevels_ServiceLevelId",
                table: "Guests",
                column: "ServiceLevelId",
                principalTable: "ServiceLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GuestServiceEntries_Guests_GuestId",
                table: "GuestServiceEntries",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GuestSessions_Guests_GuestId",
                table: "GuestSessions",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_Guests_GuestId",
                table: "Invitations",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SeatAssigns_Guests_GuestId",
                table: "SeatAssigns",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transports_Guests_GuestId",
                table: "Transports",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
