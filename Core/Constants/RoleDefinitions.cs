using System;

namespace Core.Constants;

/// <summary>
/// Built-in DMS roles. What each role can SEE and CHANGE is not listed here any
/// more — that lives in RolePermissions, managed through the Role Access screen,
/// with the Permissions table (one row per menu/submenu) as the source of truth.
/// This is only the role roster: code, display name, description, whether the role
/// may sign into the portal, and whether a delegate can hold it.
/// </summary>
public static class RoleDefinitions
{
    // PortalAccess defaults to true; only roles that must never sign into the
    // admin portal (driver, guest) pass false explicitly. IsDelegateRole defaults
    // to false — it marks the roles offered when nominating someone to a mission,
    // so nomination lists those rather than every portal role.
    public sealed record RoleDef(
        string Code,
        string Name,
        string Description,
        bool PortalAccess = true,
        bool IsDelegateRole = false);

    public static readonly RoleDef[] All =
    {
        new("protocol-officer", "Protocol Officer", "Logs invitations, creates missions, owns host communication and publishes the mission report"),
        new("mission-coordinator", "Mission Coordinator", "Assembles the roster, records logistics, runs readiness and manages on-ground operations"),
        new("department-head", "Department Head", "Nominates staff from their own department"),
        new("hr-administrator", "HR Administrator", "Verifies passport, grade, visa and insurance records for the roster"),
        // A roster role as well as a portal role: the Head of Delegation travels
        // with the delegation and signs into the portal to review the mission.
        new("head-of-delegation", "Head of Delegation", "Field decisions, protocol order and review of the combined mission report", IsDelegateRole: true),
        new("delegate", "Delegate", "Travels on the mission; follows the itinerary and submits a trip report", PortalAccess: false, IsDelegateRole: true),
        new("viewer", "Viewer", "Read-only access across the modules granted to it"),
        new(Roles.DRIVER, "Driver", "Ground-transport driver with vehicle and license details on file", PortalAccess: false),
        // Auto-provisioned 1:1 with a Guest row (see GuestService.CreateGuestAsync) —
        // never created directly by an admin. No portal access: a guest only ever
        // authenticates into the VIP app via OTP, never the portal.
        new(Roles.GUEST, "Guest", "VIP guest app account, auto-provisioned alongside its Guest profile", PortalAccess: false, IsDelegateRole: true),
    };
}
