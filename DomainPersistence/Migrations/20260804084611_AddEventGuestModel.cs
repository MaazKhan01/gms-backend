using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <summary>
    /// Adds Events.GuestModel ('fixed' | 'flexible') — see
    /// Core.Constants.EventGuestModels.
    /// <para>
    /// The backfill lives here rather than in DataSeeder because seeding is
    /// currently commented out at API/Program.cs, so seeder-based data changes
    /// never run. Same reasoning as
    /// 20260803145215_SeedServiceLevelPermissionsAndBackfillTiers.
    /// </para>
    /// </summary>
    public partial class AddEventGuestModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuestModel",
                table: "Events",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                defaultValue: "flexible");

            // AddColumn's defaultValue only governs future inserts — adding a
            // nullable column leaves every existing row NULL. Code reads NULL as
            // flexible, but writing the value keeps the column queryable.
            migrationBuilder.Sql(@"
                UPDATE [Events]
                SET [GuestModel] = 'flexible'
                WHERE [GuestModel] IS NULL;");

            // Events that already have a service level catalogue were effectively
            // running the fixed model before this flag existed — the earlier
            // backfill migration assigned their guests to levels. Calling them
            // flexible would stop enforcing rules they already depend on and hide
            // the level on guests that have one, so they become 'fixed'.
            // Everything else stays flexible: the pre-service-level flow.
            migrationBuilder.Sql(@"
                UPDATE e
                SET e.[GuestModel] = 'fixed'
                FROM [Events] e
                WHERE EXISTS (
                    SELECT 1 FROM [ServiceLevels] sl
                    WHERE sl.[EventId] = e.[Id]
                      AND (sl.[IsDeleted] = 0 OR sl.[IsDeleted] IS NULL)
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuestModel",
                table: "Events");
        }
    }
}
