using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    // ============================================================================
    // Guest becomes a 1:1 profile extension of User (Guest.UserId), and every
    // Guest-only chat/notification/device table folds into the shared User-based
    // ones. See DomainPersistence/Entities/Guest.cs, SupportConversation.cs,
    // Device.cs for the entity-level rationale.
    //
    // Ordering matters here far more than in a typical migration, because this
    // one both changes the schema AND re-keys existing data across tables in the
    // same Up(): every Guests.UserId-dependent backfill/copy/remap statement
    // below runs only after Guests.UserId itself has been populated.
    // ============================================================================
    public partial class MergeGuestIntoUserAndUnifyNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupportConversations_Guests_GuestId",
                table: "SupportConversations");

            migrationBuilder.DropForeignKey(
                name: "FK_SupportMessages_Guests_GuestId",
                table: "SupportMessages");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_SupportConversations_GuestId",
                table: "SupportConversations");

            migrationBuilder.RenameColumn(
                name: "GuestId",
                table: "SupportMessages",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_SupportMessages_GuestId_SentAt",
                table: "SupportMessages",
                newName: "IX_SupportMessages_UserId_SentAt");

            migrationBuilder.RenameColumn(
                name: "GuestId",
                table: "SupportConversations",
                newName: "UserId");

            migrationBuilder.AddColumn<int>(
                name: "OtherUserId",
                table: "SupportConversations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "SupportConversations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "AdminSupport");

            // Nullable for now — backfilled below, then locked to NOT NULL once every
            // existing Guest row has a value. (No defaultValue: 0 here on purpose —
            // 0 is never a real Users.Id, and leaving rows at 0 even transiently
            // would violate FK_Guests_Users_UserId the moment it's added.)
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Guests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DeviceIdentifier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeviceModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OsVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AppVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NotificationsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastActiveAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TokenUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ------------------------------------------------------------------
            // Data migration, phase 1: one User per Guest that doesn't have one
            // yet. RoleId -> "guest" (created here too, defensively, in case this
            // runs on a database the app-startup seeder — RoleDefinitions/
            // DataSeeder — hasn't touched yet). Email/UserName are deliberately
            // left NULL: Guests.Email has no uniqueness constraint (the same
            // person can be re-invited per event), which would collide with
            // Users' filtered-unique Email index if copied over.
            // ------------------------------------------------------------------
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Code = 'guest')
BEGIN
    INSERT INTO Roles (Name, Code, Description, PortalAccess, CreatedAt)
    VALUES (N'Guest', N'guest', N'VIP guest app account, auto-provisioned alongside its Guest profile', 0, SYSUTCDATETIME());
END

DECLARE @GuestRoleId INT = (SELECT TOP 1 Id FROM Roles WHERE Code = 'guest');

DECLARE @GuestUserMap TABLE (GuestRowId INT PRIMARY KEY, NewUserId INT);

MERGE INTO Users AS U
USING (SELECT Id AS GuestRowId, FirstName, LastName FROM Guests WHERE UserId IS NULL) AS G
ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (FirstName, LastName, IsActive, RoleId, CreatedAt, IsDeleted, PublicId)
    VALUES (G.FirstName, G.LastName, 1, @GuestRoleId, SYSUTCDATETIME(), 0, NEWID())
OUTPUT G.GuestRowId, INSERTED.Id INTO @GuestUserMap(GuestRowId, NewUserId);

UPDATE g
SET g.UserId = m.NewUserId
FROM Guests g
INNER JOIN @GuestUserMap m ON g.Id = m.GuestRowId;
");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Guests",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // ------------------------------------------------------------------
            // Data migration, phase 2: fold GuestDevices into the new Devices
            // table (now that every Guest has a UserId to re-key onto), then
            // GuestNotifications into the existing Notifications table, then
            // remap SupportConversations/SupportMessages' renamed UserId column
            // from "the old Guest.Id" to "the guest's linked User.Id".
            // ------------------------------------------------------------------
            migrationBuilder.Sql(@"
INSERT INTO Devices (UserId, Token, Platform, NotificationsEnabled, IsActive, LastActiveAt, TokenUpdatedAt, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted, DeletedBy, DeletedAt, PublicId)
SELECT g.UserId, gd.Token, gd.Platform, 1, 1, gd.LastSeenAt, gd.LastSeenAt, gd.CreatedBy, gd.CreatedAt, gd.UpdatedBy, gd.UpdatedAt, gd.IsDeleted, gd.DeletedBy, gd.DeletedAt, NEWID()
FROM GuestDevices gd
INNER JOIN Guests g ON g.Id = gd.GuestId;

INSERT INTO Notifications (UserId, Title, Message, Type, [Read], RedirectUrl, Data, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, IsDeleted, DeletedBy, DeletedAt, PublicId)
SELECT g.UserId, gn.Title, gn.Message, gn.Type, gn.[Read], gn.RedirectUrl, gn.Data, gn.CreatedBy, gn.CreatedAt, gn.UpdatedBy, gn.UpdatedAt, gn.IsDeleted, gn.DeletedBy, gn.DeletedAt, NEWID()
FROM GuestNotifications gn
INNER JOIN Guests g ON g.Id = gn.GuestId;

UPDATE sc
SET sc.UserId = g.UserId
FROM SupportConversations sc
INNER JOIN Guests g ON g.Id = sc.UserId;

UPDATE sm
SET sm.UserId = g.UserId
FROM SupportMessages sm
INNER JOIN Guests g ON g.Id = sm.UserId;
");

            migrationBuilder.DropTable(
                name: "GuestDevices");

            migrationBuilder.DropTable(
                name: "GuestNotifications");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SupportConversations_OtherUserId",
                table: "SupportConversations",
                column: "OtherUserId");

            // Two filtered unique indexes, not one composite index — a guest gets
            // at most one AdminSupport thread (enforced by the first index, over
            // rows where OtherUserId IS NULL) and at most one DriverGuest thread
            // per driver (enforced by the second, over rows where it's set).
            migrationBuilder.CreateIndex(
                name: "IX_SupportConversations_UserId_Type",
                table: "SupportConversations",
                columns: new[] { "UserId", "Type" },
                unique: true,
                filter: "[OtherUserId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SupportConversations_UserId_Type_OtherUserId",
                table: "SupportConversations",
                columns: new[] { "UserId", "Type", "OtherUserId" },
                unique: true,
                filter: "[OtherUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_UserId",
                table: "Guests",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_PublicId",
                table: "Devices",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_Token",
                table: "Devices",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId_IsActive",
                table: "Devices",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_Guests_Users_UserId",
                table: "Guests",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SupportConversations_Users_OtherUserId",
                table: "SupportConversations",
                column: "OtherUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupportConversations_Users_UserId",
                table: "SupportConversations",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SupportMessages_Users_UserId",
                table: "SupportMessages",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        // Schema is fully reversible. Data is reversible for the Guest<->User
        // link and the AdminSupport SupportConversation/SupportMessage re-key
        // (remapped back below before the columns are renamed back) but NOT for:
        //   - GuestNotification/GuestDevice consolidation: by the time anyone runs
        //     Down(), the unified Notifications/Devices tables may already contain
        //     rows for guests and staff created AFTER Up() ran, with no reliable
        //     way to tell which ones originated from a pre-migration row. Down()
        //     recreates the empty tables (so the model matches) but does not
        //     attempt to split that data back out.
        //   - DriverGuest conversations (Task 6): these have no pre-migration
        //     equivalent. If any exist, recreating the old unique-per-guest index
        //     on SupportConversations.GuestId will FAIL (a guest can have an
        //     AdminSupport thread AND one DriverGuest thread per driver — no
        //     longer "at most one row per guest"). Down() is therefore only safe
        //     to run before the driver<->guest chat endpoint has been used.
        // The Users rows Up() created for each Guest are also left in place
        // rather than deleted, since other data may reference them by then.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Guests_Users_UserId",
                table: "Guests");

            migrationBuilder.DropForeignKey(
                name: "FK_SupportConversations_Users_OtherUserId",
                table: "SupportConversations");

            migrationBuilder.DropForeignKey(
                name: "FK_SupportConversations_Users_UserId",
                table: "SupportConversations");

            migrationBuilder.DropForeignKey(
                name: "FK_SupportMessages_Users_UserId",
                table: "SupportMessages");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_SupportConversations_OtherUserId",
                table: "SupportConversations");

            migrationBuilder.DropIndex(
                name: "IX_SupportConversations_UserId_Type",
                table: "SupportConversations");

            migrationBuilder.DropIndex(
                name: "IX_SupportConversations_UserId_Type_OtherUserId",
                table: "SupportConversations");

            migrationBuilder.DropIndex(
                name: "IX_Guests_UserId",
                table: "Guests");

            // Remap SupportConversations/SupportMessages' UserId (currently a
            // guest's User.Id) back to the Guest.Id it held pre-migration, while
            // the column is still named UserId, before renaming it back below.
            migrationBuilder.Sql(@"
UPDATE sc
SET sc.UserId = g.Id
FROM SupportConversations sc
INNER JOIN Guests g ON g.UserId = sc.UserId
WHERE sc.Type = 'AdminSupport';

UPDATE sm
SET sm.UserId = g.Id
FROM SupportMessages sm
INNER JOIN Guests g ON g.UserId = sm.UserId;
");

            migrationBuilder.DropColumn(
                name: "OtherUserId",
                table: "SupportConversations");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "SupportConversations");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Guests");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "SupportMessages",
                newName: "GuestId");

            migrationBuilder.RenameIndex(
                name: "IX_SupportMessages_UserId_SentAt",
                table: "SupportMessages",
                newName: "IX_SupportMessages_GuestId_SentAt");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "SupportConversations",
                newName: "GuestId");

            migrationBuilder.CreateTable(
                name: "GuestDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GuestId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValueSql: "((0))"),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Platform = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    Token = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestDevices_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuestNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GuestId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValueSql: "((0))"),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    Read = table.Column<bool>(type: "bit", nullable: false),
                    RedirectUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestNotifications_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "([IsDeleted] IS NULL OR [IsDeleted] = 0)");

            migrationBuilder.CreateIndex(
                name: "IX_SupportConversations_GuestId",
                table: "SupportConversations",
                column: "GuestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestDevices_GuestId",
                table: "GuestDevices",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestDevices_PublicId",
                table: "GuestDevices",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestDevices_Token",
                table: "GuestDevices",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestNotifications_GuestId_Read",
                table: "GuestNotifications",
                columns: new[] { "GuestId", "Read" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestNotifications_PublicId",
                table: "GuestNotifications",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SupportConversations_Guests_GuestId",
                table: "SupportConversations",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SupportMessages_Guests_GuestId",
                table: "SupportMessages",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
