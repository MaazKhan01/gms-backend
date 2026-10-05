using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupsLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "EventGuests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_Groups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventGuests_EventId_GroupId",
                table: "EventGuests",
                columns: new[] { "EventId", "GroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventGuests_GroupId",
                table: "EventGuests",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_Name",
                table: "Groups",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_PublicId",
                table: "Groups",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EventGuests_Groups_GroupId",
                table: "EventGuests",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ── Backfill: the free-text sub-groups become real rows ──────────
            // EventGuests.Subgroup has been free text until now, so the existing
            // values ARE the groups. Collapsed on a trimmed name (SQL Server's
            // default collation is case-insensitive, so "Team A" and "team a"
            // become one row — which is the whole point of replacing free text).
            //
            // Subgroup itself is deliberately left in place and still written:
            // Readiness, On-Mission Ops and Incidents all group by the string,
            // and it is now mirrored from the group's name — the same
            // arrangement Guest.Tier has with ServiceLevel.Code.
            migrationBuilder.Sql(@"
                INSERT INTO dbo.Groups (Name, CreatedAt, IsDeleted)
                SELECT DISTINCT LTRIM(RTRIM(eg.Subgroup)), SYSUTCDATETIME(), 0
                FROM dbo.EventGuests eg
                WHERE eg.Subgroup IS NOT NULL
                  AND LTRIM(RTRIM(eg.Subgroup)) <> ''
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.Groups g
                      WHERE g.Name = LTRIM(RTRIM(eg.Subgroup)) AND g.IsDeleted = 0);");

            migrationBuilder.Sql(@"
                UPDATE eg SET eg.GroupId = g.Id
                FROM dbo.EventGuests eg
                JOIN dbo.Groups g ON g.Name = LTRIM(RTRIM(eg.Subgroup)) AND g.IsDeleted = 0
                WHERE eg.GroupId IS NULL AND eg.Subgroup IS NOT NULL;");

            // ── The menu row ────────────────────────────────────────────────
            // Startup seeding is disabled (Program.cs), so a new menu has to be
            // inserted by migration or it simply never appears. Idempotent, the
            // same shape docs/dms-seed-part2.sql uses.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = 'lookup-groups')
                INSERT INTO dbo.Permissions
                    (Code, Name, NameAr, Description, Icon, [Path], SortOrder, IsActive, CreatedAt, IsDeleted, PublicId)
                VALUES
                    ('lookup-groups', N'Groups', N'المجموعات', N'Reference data',
                     'guests', '/lookups/groups', 1268, 1, SYSUTCDATETIME(), 0, NEWID());");

            migrationBuilder.Sql(@"
                UPDATE c SET ParentId = p.Id
                FROM dbo.Permissions c
                JOIN dbo.Permissions p ON p.Code = 'lookups' AND p.IsDeleted = 0
                WHERE c.Code = 'lookup-groups' AND c.IsDeleted = 0 AND c.ParentId IS NULL;");

            // Granted to whoever already holds the Lookups section, at the same
            // read/write level — a new lookup is not a new privilege.
            migrationBuilder.Sql(@"
                INSERT INTO dbo.RolePermissions
                    (RoleId, PermissionId, CreatedAt, IsDeleted, PublicId, CanRead, CanWrite)
                SELECT rp.RoleId, child.Id, SYSUTCDATETIME(), 0, NEWID(), rp.CanRead, rp.CanWrite
                FROM dbo.RolePermissions rp
                JOIN dbo.Permissions parent ON parent.Id = rp.PermissionId
                                           AND parent.Code = 'lookups' AND parent.IsDeleted = 0
                CROSS JOIN (SELECT Id FROM dbo.Permissions
                            WHERE Code = 'lookup-groups' AND IsDeleted = 0) child
                WHERE rp.IsDeleted = 0
                  AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions x
                                  WHERE x.RoleId = rp.RoleId AND x.PermissionId = child.Id
                                    AND x.IsDeleted = 0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The menu goes with it. The backfilled Groups rows and the GroupId
            // values need no undo of their own — dropping the table and the
            // column below takes them, and Subgroup still holds every name.
            migrationBuilder.Sql(@"
                DELETE rp FROM dbo.RolePermissions rp
                JOIN dbo.Permissions p ON p.Id = rp.PermissionId
                WHERE p.Code = 'lookup-groups';
                DELETE FROM dbo.Permissions WHERE Code = 'lookup-groups';");

            migrationBuilder.DropForeignKey(
                name: "FK_EventGuests_Groups_GroupId",
                table: "EventGuests");

            migrationBuilder.DropTable(
                name: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_EventGuests_EventId_GroupId",
                table: "EventGuests");

            migrationBuilder.DropIndex(
                name: "IX_EventGuests_GroupId",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "EventGuests");
        }
    }
}
