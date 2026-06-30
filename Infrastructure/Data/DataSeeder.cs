using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Core.Common;
using Core.Constants;
using DomainPersistence.Entities;

namespace Infrastructure.Data;

/// <summary>
/// Idempotent startup seeder. Safe to run on every boot:
///   1. Applies pending EF migrations (creates the database if missing).
///   2. Syncs the Permission catalog from <see cref="PermissionCodes"/>.
///   3. Ensures the built-in roles (admin / user) exist.
///   4. Grants every permission to the admin role.
///   5. Creates a default admin user (credentials from the "Seed" config section).
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(
        ApplicationDBContext db,
        IConfiguration config,
        ILogger logger = null,
        CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        await SyncPermissionsAsync(db, ct);
        var adminRole = await EnsureRoleAsync(db, Roles.ADMIN, "Administrator", "Full system access", ct);
        await EnsureRoleAsync(db, Roles.USER, "User", "Standard user", ct);
        await GrantAllPermissionsAsync(db, adminRole, ct);
        await SeedDefinedRolesAsync(db, ct);
        await EnsureAdminUserAsync(db, config, adminRole, logger, ct);

        await db.SaveChangesAsync(ct);
        logger?.LogInformation("Data seeding completed.");
    }

    // 1+2. Ensure a Permission row exists for every code declared in PermissionCodes.
    private static async Task SyncPermissionsAsync(ApplicationDBContext db, CancellationToken ct)
    {
        var codes = typeof(PermissionCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct()
            .ToList();

        var existing = await db.Permissions.Select(p => p.Code).ToListAsync(ct);
        var existingSet = new HashSet<string>(existing);

        foreach (var code in codes.Where(c => !existingSet.Contains(c)))
        {
            var parts = code.Split('.', 2);
            var module = parts.Length > 0 ? parts[0] : code;
            var action = parts.Length > 1 ? parts[1] : code;
            db.Permissions.Add(new Permission
            {
                Id = Guid.NewGuid(),
                Code = code,
                Module = module,
                Name = $"{module} · {action}",
                Description = $"Allows {action} in {module}",
            });
        }
    }

    private static async Task<Role> EnsureRoleAsync(
        ApplicationDBContext db, string code, string name, string description, CancellationToken ct)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (role == null)
        {
            role = new Role { Id = Guid.NewGuid(), Code = code, Name = name, Description = description };
            db.Roles.Add(role);
        }
        return role;
    }

    // 4. Make sure the admin role is linked to every permission.
    private static async Task GrantAllPermissionsAsync(ApplicationDBContext db, Role adminRole, CancellationToken ct)
    {
        var allPermissionIds = await db.Permissions.Select(p => p.Id).ToListAsync(ct);

        // Include freshly-added (not-yet-saved) permissions tracked in the change tracker.
        var pendingPermissionIds = db.ChangeTracker.Entries<Permission>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity.Id);
        var permissionIds = allPermissionIds.Concat(pendingPermissionIds).Distinct().ToList();

        var linkedIds = await db.RolePermissions
            .Where(rp => rp.RoleId == adminRole.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync(ct);
        var linkedSet = new HashSet<Guid>(linkedIds);

        foreach (var permissionId in permissionIds.Where(id => !linkedSet.Contains(id)))
        {
            db.RolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = adminRole.Id,
                PermissionId = permissionId,
            });
        }
    }

    // Build a code -> id map including not-yet-saved (tracked) permissions.
    private static Dictionary<string, Guid> PermissionMap(ApplicationDBContext db)
    {
        var map = db.Permissions.AsNoTracking().ToDictionary(p => p.Code, p => p.Id);
        foreach (var e in db.ChangeTracker.Entries<Permission>().Where(e => e.State == EntityState.Added))
            map[e.Entity.Code] = e.Entity.Id;
        return map;
    }

    // Seed the built-in GMS roles. Create-if-missing only: an existing role's
    // permissions are left untouched so admin edits are preserved.
    private static async Task SeedDefinedRolesAsync(ApplicationDBContext db, CancellationToken ct)
    {
        var existingCodes = new HashSet<string>(await db.Roles.Select(r => r.Code).ToListAsync(ct));
        var permMap = PermissionMap(db);

        foreach (var def in RoleDefinitions.All.Where(d => !existingCodes.Contains(d.Code)))
        {
            var role = new Role { Id = Guid.NewGuid(), Code = def.Code, Name = def.Name, Description = def.Description };
            db.Roles.Add(role);

            foreach (var code in def.Permissions.Distinct())
                if (permMap.TryGetValue(code, out var pid))
                    db.RolePermissions.Add(new RolePermission { Id = Guid.NewGuid(), RoleId = role.Id, PermissionId = pid });
        }
    }

    // 5. Create the default admin user if no account with that email exists.
    private static async Task EnsureAdminUserAsync(
        ApplicationDBContext db, IConfiguration config, Role adminRole, ILogger logger, CancellationToken ct)
    {
        var email = config["Seed:AdminEmail"] ?? "admin@gms.local";
        var password = config["Seed:AdminPassword"] ?? "Admin@123!";
        var userName = config["Seed:AdminUserName"] ?? "admin";

        var exists = await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct);
        if (exists) return;

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = email,
            FirstName = "System",
            LastName = "Administrator",
            IsActive = true,
            RoleId = adminRole.Id,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        });

        logger?.LogWarning("Seeded admin user '{Email}'. Change the seeded password after first login.", email);
    }
}
