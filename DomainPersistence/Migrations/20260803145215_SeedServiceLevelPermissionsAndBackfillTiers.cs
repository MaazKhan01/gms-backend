using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <summary>
    /// Data migration for the Service Levels feature:
    ///   1. Registers the 5 new permission codes and grants them to the admin role.
    ///   2. Backfills the legacy Guest.Tier strings into real per-event ServiceLevel
    ///      rows and repoints the guests at them.
    ///
    /// Both would normally be handled by DataSeeder (which reflects over
    /// PermissionCodes on every boot), but its call is currently commented out in
    /// Program.cs — so a migration is the only thing guaranteed to run on deploy.
    /// Every statement is re-runnable, so it stays correct if seeding is switched
    /// back on later and does the same work.
    /// </summary>
    public partial class SeedServiceLevelPermissionsAndBackfillTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Permission rows + admin grants ────────────────────────────
            // Name/Description mirror DataSeeder.SyncPermissionsAsync's format
            // ("Module · Action" / "Allows Action in Module") so rows created here
            // are indistinguishable from seeded ones.
            migrationBuilder.Sql(@"
                DECLARE @codes TABLE (Code nvarchar(100) PRIMARY KEY);
                INSERT INTO @codes (Code) VALUES
                    ('Services.View'), ('Services.Manage'),
                    ('ServiceLevels.View'), ('ServiceLevels.Manage'),
                    ('ServiceLevels.OverrideRules');

                INSERT INTO Permissions (Code, Module, Name, Description)
                SELECT
                    c.Code,
                    LEFT(c.Code, CHARINDEX('.', c.Code) - 1),
                    LEFT(c.Code, CHARINDEX('.', c.Code) - 1) + N' · ' + SUBSTRING(c.Code, CHARINDEX('.', c.Code) + 1, 200),
                    N'Allows ' + SUBSTRING(c.Code, CHARINDEX('.', c.Code) + 1, 200)
                        + N' in ' + LEFT(c.Code, CHARINDEX('.', c.Code) - 1)
                FROM @codes c
                WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.Code = c.Code);

                -- Grant to the admin role, matching DataSeeder.GrantAllPermissionsAsync.
                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT r.Id, p.Id
                FROM Roles r
                CROSS JOIN Permissions p
                WHERE r.Code = 'admin'
                  AND p.Code IN (SELECT Code FROM @codes)
                  AND NOT EXISTS (
                        SELECT 1 FROM RolePermissions rp
                        WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
            ");

            // ── 2. Tier -> ServiceLevel backfill ─────────────────────────────
            // One level per (event, tier-actually-in-use). Known tiers get their
            // canonical name/colour/rank; anything else (hand-edited or CSV-imported)
            // is title-cased and sorted last, so no data is silently dropped.
            migrationBuilder.Sql(@"
                DECLARE @tiers TABLE (
                    Code nvarchar(50) PRIMARY KEY,
                    Name nvarchar(200),
                    NameAr nvarchar(200),
                    Color nvarchar(20),
                    SortOrder int
                );
                INSERT INTO @tiers (Code, Name, NameAr, Color, SortOrder) VALUES
                    ('vvip',     N'VVIP',     N'شخصية مهمة جداً', '#e0b864', 1),
                    ('vip',      N'VIP',      N'شخصية مهمة',      '#a78bda', 2),
                    ('speaker',  N'Speaker',  N'متحدث',           '#8d0134', 3),
                    ('delegate', N'Delegate', N'مندوب',           '#5abf6e', 4),
                    ('press',    N'Press',    N'صحافة',           '#e08a7e', 5),
                    ('observer', N'Observer', N'مراقب',           '#9CA3AF', 6);

                -- Distinct (event, tier) pairs that still need a level.
                WITH needed AS (
                    SELECT DISTINCT g.EventId, LOWER(LTRIM(RTRIM(g.Tier))) AS Code
                    FROM Guests g
                    WHERE g.ServiceLevelId IS NULL
                      AND g.Tier IS NOT NULL AND LTRIM(RTRIM(g.Tier)) <> ''
                      AND (g.IsDeleted IS NULL OR g.IsDeleted = 0)
                )
                INSERT INTO ServiceLevels
                    (EventId, Name, NameAr, Code, Color, SortOrder, Description, IsDeleted)
                SELECT
                    n.EventId,
                    COALESCE(t.Name, UPPER(LEFT(n.Code, 1)) + SUBSTRING(n.Code, 2, 200)),
                    t.NameAr,
                    n.Code,
                    COALESCE(t.Color, '#9CA3AF'),
                    COALESCE(t.SortOrder, 99),
                    N'Migrated from the legacy guest tier.',
                    0
                FROM needed n
                LEFT JOIN @tiers t ON t.Code = n.Code
                WHERE NOT EXISTS (
                    SELECT 1 FROM ServiceLevels sl
                    WHERE sl.EventId = n.EventId
                      AND LOWER(sl.Code) = n.Code
                      AND (sl.IsDeleted IS NULL OR sl.IsDeleted = 0));
            ");

            // Repoint the guests, and normalise Tier onto the level's code so the
            // mirrored string is consistent from here on (it was mixed-case before).
            migrationBuilder.Sql(@"
                UPDATE g
                SET g.ServiceLevelId = sl.Id,
                    g.Tier           = sl.Code
                FROM Guests g
                INNER JOIN ServiceLevels sl
                    ON sl.EventId = g.EventId
                   AND LOWER(sl.Code) = LOWER(LTRIM(RTRIM(g.Tier)))
                   AND (sl.IsDeleted IS NULL OR sl.IsDeleted = 0)
                WHERE g.ServiceLevelId IS NULL
                  AND g.Tier IS NOT NULL AND LTRIM(RTRIM(g.Tier)) <> ''
                  AND (g.IsDeleted IS NULL OR g.IsDeleted = 0);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Detach guests from the migrated levels, then remove the levels this
            // migration created and the permissions it registered. Guest.Tier is
            // deliberately left intact — it's the pre-migration source of truth.
            migrationBuilder.Sql(@"
                UPDATE g SET g.ServiceLevelId = NULL
                FROM Guests g
                INNER JOIN ServiceLevels sl ON sl.Id = g.ServiceLevelId
                WHERE sl.Description = N'Migrated from the legacy guest tier.';

                DELETE FROM ServiceLevels
                WHERE Description = N'Migrated from the legacy guest tier.';

                DELETE rp FROM RolePermissions rp
                INNER JOIN Permissions p ON p.Id = rp.PermissionId
                WHERE p.Code IN ('Services.View','Services.Manage',
                                 'ServiceLevels.View','ServiceLevels.Manage',
                                 'ServiceLevels.OverrideRules');

                DELETE FROM Permissions
                WHERE Code IN ('Services.View','Services.Manage',
                               'ServiceLevels.View','ServiceLevels.Manage',
                               'ServiceLevels.OverrideRules');
            ");
        }
    }
}
