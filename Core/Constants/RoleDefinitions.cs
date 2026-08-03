using System;
using Core.Common;

namespace Core.Constants;

/// <summary>
/// Built-in GMS roles and their default permission sets. The seeder creates any
/// missing role with these permissions; existing roles are left untouched so
/// admin edits via the Roles UI are not clobbered. "Manager" naming throughout.
/// </summary>
public static class RoleDefinitions
{
    // PortalAccess defaults to true so every pre-existing RoleDef call site (none
    // of which pass it) keeps its current behavior. Only roles that must never
    // sign into the admin portal (driver, guest) pass false explicitly.
    public sealed record RoleDef(string Code, string Name, string Description, string[] Permissions, bool PortalAccess = true);

    // Every read-only ("view") permission across modules — used by Viewer and
    // mixed into manager roles that need to see neighbouring modules.
    public static readonly string[] AllViews =
    {
        PermissionCodes.EventsView, PermissionCodes.InvitationsView, PermissionCodes.GuestsView,
        PermissionCodes.TravelView, PermissionCodes.TransportationView, PermissionCodes.AccreditationView, PermissionCodes.VenueView,
        PermissionCodes.SeatingView, PermissionCodes.MeetingsView, PermissionCodes.ProtocolView,
        PermissionCodes.FinancialsView, PermissionCodes.ReportsView, PermissionCodes.DashboardView,
    };

    public static readonly RoleDef[] All =
    {
        // Each role owns ONLY its core module + Dashboard.
        // Cross-module visibility is granted per-user by admin via User Access.

        new("event-manager", "Event Manager", "Manage events and sessions", new[]
        {
            PermissionCodes.EventsView, PermissionCodes.EventsCreate, PermissionCodes.EventsUpdate,
            PermissionCodes.EventsDelete, PermissionCodes.EventsManageStatus, PermissionCodes.EventsManageSessions,
            PermissionCodes.EventsImport,
            PermissionCodes.DashboardView,
        }),
        new("invitations-manager", "Invitations Manager", "Design templates and send invitations", new[]
        {
            PermissionCodes.InvitationsView, PermissionCodes.InvitationsManageTemplates,
            PermissionCodes.InvitationsSend, PermissionCodes.DashboardView,
        }),
        new("guest-relations-manager", "Guest Relations Manager", "Manage guests and registration", new[]
        {
            PermissionCodes.GuestsView, PermissionCodes.GuestsCreate, PermissionCodes.GuestsUpdate,
            PermissionCodes.GuestsDelete, PermissionCodes.GuestsImport, PermissionCodes.GuestsExport,
            PermissionCodes.DashboardView,
        }),
        new("travel-manager", "Travel & Logistics Manager", "Manage flights, hotels, transfers and visas", new[]
        {
            PermissionCodes.TravelView, PermissionCodes.TravelManage, PermissionCodes.TravelSyncHayya,
            // Transportation module (driver assignment, schedules, on-demand
            // requests) is the same "transfers" concern this role already owns.
            PermissionCodes.TransportationView, PermissionCodes.TransportationManage, PermissionCodes.TransportationAssign,
            PermissionCodes.DashboardView,
        }),
        new("accreditation-manager", "Accreditation Manager", "Issue and revoke accreditation badges", new[]
        {
            PermissionCodes.AccreditationView, PermissionCodes.AccreditationIssue,
            PermissionCodes.AccreditationRevoke, PermissionCodes.DashboardView,
        }),
        new("seating-manager", "Seating Manager", "Assign guests to seats on the floor plan", new[]
        {
            PermissionCodes.SeatingView, PermissionCodes.SeatingAssign, PermissionCodes.DashboardView,
        }),
        new("venue-manager", "Venue Manager", "Configure venues and floor plans", new[]
        {
            PermissionCodes.VenueView, PermissionCodes.VenueManage, PermissionCodes.DashboardView,
        }),
        new("protocol-manager", "Protocol Manager", "Manage protocol notes, checklists and meetings", new[]
        {
            // Protocol and Meetings are tightly coupled — kept together.
            PermissionCodes.ProtocolView, PermissionCodes.ProtocolManage, PermissionCodes.ProtocolChecklist,
            PermissionCodes.MeetingsView, PermissionCodes.MeetingsManage, PermissionCodes.DashboardView,
        }),
        new("finance-manager", "Finance Manager", "Manage budgets, transactions and reports", new[]
        {
            PermissionCodes.FinancialsView, PermissionCodes.FinancialsManage,
            PermissionCodes.ReportsView, PermissionCodes.ReportsGenerate, PermissionCodes.DashboardView,
        }),
        new("viewer", "Viewer", "Read-only access across all modules", AllViews),
        new(Roles.DRIVER, "Driver", "Ground-transport driver with vehicle and license details on file", new[]
        {
            PermissionCodes.DashboardView,
        }, PortalAccess: false),
        // Auto-provisioned 1:1 with a Guest row (see GuestService.CreateGuestAsync) —
        // never created directly by an admin. No portal access, no permissions:
        // a guest only ever authenticates into the VIP app via OTP, never the portal.
        new(Roles.GUEST, "Guest", "VIP guest app account, auto-provisioned alongside its Guest profile", Array.Empty<string>(), PortalAccess: false),
    };
}
