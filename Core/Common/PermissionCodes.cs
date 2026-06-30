namespace Core.Common;

public static class PermissionCodes
{
    // ── Admin / platform ────────────────────────────────────────────────
    // User permissions
    public const string UsersCreate = "Users.Create";
    public const string UsersView = "Users.View";
    public const string UsersUpdate = "Users.Update";
    public const string UsersDelete = "Users.Delete";

    // Role permissions
    public const string RolesManage = "Roles.Manage";
    public const string RolesView = "Roles.View";

    // Logs permissions
    public const string LogsView = "Logs.View";

    // Account requests (self-service sign-up awaiting admin approval)
    public const string AccountRequestsView = "AccountRequests.View";
    public const string AccountRequestsManage = "AccountRequests.Manage";

    // ── GMS modules ─────────────────────────────────────────────────────
    // Each module exposes a `.View` (read-only, no action buttons) plus its
    // action permissions. Policies register automatically via reflection in
    // ServiceExtensions.ConfigureAuthorization — adding a constant is enough.

    // Events & Sessions
    public const string EventsView = "Events.View";
    public const string EventsCreate = "Events.Create";
    public const string EventsUpdate = "Events.Update";
    public const string EventsDelete = "Events.Delete";
    public const string EventsManageStatus = "Events.ManageStatus";
    public const string EventsManageSessions = "Events.ManageSessions";

    // Invitations
    public const string InvitationsView = "Invitations.View";
    public const string InvitationsManageTemplates = "Invitations.ManageTemplates";
    public const string InvitationsSend = "Invitations.Send";

    // Guests
    public const string GuestsView = "Guests.View";
    public const string GuestsCreate = "Guests.Create";
    public const string GuestsUpdate = "Guests.Update";
    public const string GuestsDelete = "Guests.Delete";
    public const string GuestsImport = "Guests.Import";
    public const string GuestsExport = "Guests.Export";

    // Travel & Logistics
    public const string TravelView = "Travel.View";
    public const string TravelManage = "Travel.Manage";
    public const string TravelSyncHayya = "Travel.SyncHayya";

    // Accreditation
    public const string AccreditationView = "Accreditation.View";
    public const string AccreditationIssue = "Accreditation.Issue";
    public const string AccreditationRevoke = "Accreditation.Revoke";

    // Venue configuration
    public const string VenueView = "Venue.View";
    public const string VenueManage = "Venue.Manage";

    // Seating
    public const string SeatingView = "Seating.View";
    public const string SeatingAssign = "Seating.Assign";

    // Meetings
    public const string MeetingsView = "Meetings.View";
    public const string MeetingsManage = "Meetings.Manage";

    // Protocol
    public const string ProtocolView = "Protocol.View";
    public const string ProtocolManage = "Protocol.Manage";
    public const string ProtocolChecklist = "Protocol.Checklist";

    // Financials
    public const string FinancialsView = "Financials.View";
    public const string FinancialsManage = "Financials.Manage";

    // Reports
    public const string ReportsView = "Reports.View";
    public const string ReportsGenerate = "Reports.Generate";

    // Dashboards
    public const string DashboardView = "Dashboard.View";
}
