using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <summary>
    /// Retires the `protocol` and `reports` menus: neither is part of DMS.
    ///
    /// Deactivated rather than deleted. `IsActive = 0` is the documented way to
    /// retire a menu — the token skips inactive rows when it is minted and
    /// GET /role-access/me filters on the same flag, so the menu disappears and
    /// its endpoints stop being grantable, without having to clean up every
    /// RolePermission that pointed at it. Deleting the row would cascade into
    /// those grants and lose the record that the access was ever held.
    /// </summary>
    public partial class RetireProtocolAndReportsMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE dbo.Permissions
                SET IsActive = 0
                WHERE Code IN ('protocol', 'reports') AND IsDeleted = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE dbo.Permissions
                SET IsActive = 1
                WHERE Code IN ('protocol', 'reports') AND IsDeleted = 0;");
        }
    }
}
