using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <summary>
    /// Retires the `account-requests` menu. Users are added by invitation from
    /// the Users screen, so an approval queue for self-service sign-ups has
    /// nothing to hold.
    ///
    /// Deactivated, not deleted, for the same reason as
    /// <see cref="RetireProtocolAndReportsMenus"/>: IsActive = 0 removes the
    /// menu and stops the endpoints being grantable without cascading away the
    /// RolePermission rows that recorded who once held the access.
    ///
    /// The controller and its endpoints stay. Nothing calls them once the menu
    /// is gone, but any request that somehow arrives is still authorised
    /// against a permission no active role can hold.
    /// </summary>
    public partial class RetireAccountRequestsMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE dbo.Permissions
                SET IsActive = 0
                WHERE Code = 'account-requests' AND IsDeleted = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE dbo.Permissions
                SET IsActive = 1
                WHERE Code = 'account-requests' AND IsDeleted = 0;");
        }
    }
}
