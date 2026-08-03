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

    // Per-user module access grants (admin only)
    public const string UserAccessManage = "UserAccess.Manage";

    // Lookups (generic reference data — admin managed)
    public const string LookupsView = "Lookups.View";
    public const string LookupsManage = "Lookups.Manage";

    // Organizations (admin managed; the read endpoint is open to any signed-in
    // user so every module can populate an organisation dropdown).
    public const string OrganizationsView = "Organizations.View";
    public const string OrganizationsManage = "Organizations.Manage";

    // Per-event service catalog (admin managed). Reads are open to any signed-in
    // user so the Service Levels builder and guest form can populate dropdowns.
    public const string ServicesView = "Services.View";
    public const string ServicesManage = "Services.Manage";

    // Per-event guest grades — replaces the old hardcoded tier list.
    public const string ServiceLevelsView = "ServiceLevels.View";
    public const string ServiceLevelsManage = "ServiceLevels.Manage";
    // Lets a user push a guest onto a level whose rules fail (capacity full, or
    // required guest fields missing). Deliberately separate from .Manage so
    // "can edit levels" and "can waive the rules" are grantable independently.
    public const string ServiceLevelsOverrideRules = "ServiceLevels.OverrideRules";

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
    public const string EventsImport = "Events.Import";

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

    // Transportation — driver assignment & guest ride requests
    public const string TransportationView = "Transportation.View";
    public const string TransportationManage = "Transportation.Manage";
    public const string TransportationAssign = "Transportation.Assign";

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

    // Support chat (guest ↔ admin)
    public const string SupportChatView = "SupportChat.View";
    public const string SupportChatManage = "SupportChat.Manage";

    // Notifications (admin-triggered sends to other users — everyone can read/manage their own)
    public const string NotificationsSend = "Notifications.Send";
}
