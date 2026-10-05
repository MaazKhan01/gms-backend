using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <summary>
    /// The account auto-provisioned alongside a person's Guest profile is, in
    /// DMS, a DELEGATE's account. "Guest" is GMS vocabulary that survived the
    /// port and surfaced on the Users screen, where every delegate's role read
    /// "Guest".
    ///
    /// Renaming rather than re-pointing the users at a different role: the row
    /// already means exactly the right thing, it was just labelled with the old
    /// domain's word. The CODE stays `guest` because GuestService and
    /// NominationService look it up by code, and a code is an identifier, not a
    /// label.
    ///
    /// IsDelegateRole is cleared at the same time. That flag feeds the MISSION
    /// role dropdown (Head of Delegation / Member / Support Staff) — what
    /// someone does on the mission, which is a different question from what
    /// their login can do. Leaving it set offered "Guest" as a mission role.
    /// </summary>
    public partial class RenameGuestRoleToDelegate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE dbo.Roles
                SET Name = N'Delegate',
                    Description = N'Delegate account, auto-provisioned alongside the person''s delegate profile',
                    IsDelegateRole = 0
                WHERE Code = 'guest' AND IsDeleted = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE dbo.Roles
                SET Name = N'Guest',
                    Description = N'VIP guest app account, auto-provisioned alongside its Guest profile',
                    IsDelegateRole = 1
                WHERE Code = 'guest' AND IsDeleted = 0;");
        }
    }
}
