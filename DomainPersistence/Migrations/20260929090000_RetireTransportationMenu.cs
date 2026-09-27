using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <summary>
    /// Retires the `transportation` menu. Transport in DMS is booked per
    /// delegate from Services, not planned as a schedule of its own, so the
    /// menu had nothing behind it.
    ///
    /// Deactivated rather than deleted, for the same reason as the other
    /// Retire* migrations: IsActive = 0 removes the menu and stops the
    /// permission being grantable without cascading away the RolePermission
    /// rows that recorded the access was once held.
    /// </summary>
    public partial class RetireTransportationMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE dbo.Permissions
                SET IsActive = 0
                WHERE Code = 'transportation' AND IsDeleted = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE dbo.Permissions
                SET IsActive = 1
                WHERE Code = 'transportation' AND IsDeleted = 0;");
        }
    }
}
