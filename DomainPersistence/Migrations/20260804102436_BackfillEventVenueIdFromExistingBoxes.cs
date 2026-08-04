using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillEventVenueIdFromExistingBoxes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Events created before Event.VenueId was actually populated on
            // create/update (and before VenueService.CreateVenueBoxAsync started
            // backfilling it when a layout is saved) can have a real VenueBox but
            // VenueId still NULL — which breaks SeatingView's venue derivation
            // (no event-level venue to fall back to when no session is selected).
            // Idempotent: only touches events where VenueId IS NULL, preferring
            // each event's own event-level box (SessionId IS NULL) over a
            // session-scoped one when both exist.
            migrationBuilder.Sql(@"
                UPDATE e
                SET e.VenueId = v.Id, e.VenueName = v.Name
                FROM Events e
                CROSS APPLY (
                    SELECT TOP 1 vb.VenueId
                    FROM VenueBoxes vb
                    WHERE vb.EventId = e.Id
                    ORDER BY CASE WHEN vb.SessionId IS NULL THEN 0 ELSE 1 END, vb.Id
                ) best
                JOIN Venues v ON v.Id = best.VenueId
                WHERE e.VenueId IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately irreversible — we can't tell backfilled rows apart
            // from ones where VenueId was already set for real.
        }
    }
}
