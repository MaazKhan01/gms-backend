namespace Core.Common;

/// <summary>
/// The Permissions.Code values referenced by <c>[HasPermission]</c> on endpoints.
/// The database owns the menu TREE (labels, paths, order, parentage, which rows
/// exist) — this is only a compile-time spelling check for the codes the API
/// gates on, so a typo is a build error rather than a silent 403. Adding a
/// permission row that no endpoint gates needs no entry here.
///
/// These replaced the old action codes ("Users.Create", "Guests.View"): the model
/// is now one row per MENU with independent Read/Write flags, so the verb moved
/// out of the code and onto the attribute — <c>[HasPermission(Users)]</c> for a
/// read, <c>[HasPermission(Users, AccessLevel.Write)]</c> for anything that changes
/// something.
/// </summary>
public static class PermissionCodes
{
    // ── Section / group rows (no page of their own) ──────────────────────
    public const string SectionMission = "mission";
    public const string SectionDelegation = "delegation";
    public const string SectionHostCommunication = "host-communication";
    public const string SectionEvent = "event";
    public const string SectionPreDeparture = "pre-departure";
    public const string SectionActiveMission = "active-mission";
    public const string SectionReports = "reports-close";
    public const string SectionOnsite = "onsite";
    public const string SectionVenueManagement = "venue-management";
    public const string SectionFleet = "fleet";
    public const string SectionAccommodation = "accommodation";
    public const string SectionAdmin = "admin";
    public const string SectionUserManagement = "user-management";

    // ── MISSIONS ─────────────────────────────────────────────────────────
    public const string Dashboard = "dashboard";
    /// <summary>Invitations logged from host organisations (Phase 1).</summary>
    public const string ExternalInvitations = "external-invitations";
    /// <summary>The missions themselves — served by EventsController, since an
    /// Event IS a mission.</summary>
    public const string Events = "events";

    // ── DELEGATION ASSEMBLY ──────────────────────────────────────────────
    /// <summary>Delegates on a mission. Labelled "Delegates" in the UI; the code
    /// stays "guests" because that is what the entity and endpoints are called.</summary>
    public const string Guests = "guests";
    public const string Nominations = "nominations";
    public const string HrVerification = "hr-verification";

    // ── HOST COMMUNICATION ───────────────────────────────────────────────
    public const string NominationLetter = "nomination-letter";

    // ── EVENT ────────────────────────────────────────────────────────────
    /// <summary>The one Services page: flights, accommodation, transport, visa.</summary>
    public const string Services = "services";
    public const string SupportChat = "support-chat";
    public const string Transportation = "transportation";

    // ── PRE-DEPARTURE ────────────────────────────────────────────────────
    public const string Readiness = "readiness";

    // ── ACTIVE MISSION ───────────────────────────────────────────────────
    public const string OnMissionOps = "on-mission-ops";
    public const string Incidents = "incidents";
    public const string HeadOfDelegation = "head-of-delegation";

    // ── REPORTS & CLOSE ──────────────────────────────────────────────────
    public const string PostMissionReports = "post-mission-reports";
    public const string CombinedReport = "combined-report";
    public const string Reports = "reports";

    // ── ON-SITE ──────────────────────────────────────────────────────────
    public const string Accreditation = "accreditation";
    public const string Seating = "seating";
    public const string Meetings = "meetings";
    public const string Protocol = "protocol";

    // ── VENUE MANAGEMENT ─────────────────────────────────────────────────
    public const string VenueConfig = "venue-config";
    public const string Venues = "venues";

    // ── FLEET ────────────────────────────────────────────────────────────
    public const string Vehicles = "vehicles";
    public const string FleetProviders = "fleet-providers";
    public const string FleetBookings = "fleet-bookings";

    // ── ACCOMMODATION ────────────────────────────────────────────────────
    public const string RoomInventory = "room-inventory";

    // ── ADMIN ────────────────────────────────────────────────────────────
    public const string TemplateBuilder = "template-builder";
    public const string GuestOverview = "guest-overview";
    public const string Organizations = "organizations";
    public const string ServiceLevels = "service-levels";
    public const string ManageServices = "manage-services";
    /// <summary>Parent of the individual lookup screens. A group row with no page
    /// of its own — each lookup screen owns its own code, so a role can be given
    /// one reference list without the rest.</summary>
    public const string Lookups = "lookups";
    public const string Logs = "logs";
    /// <summary>Financials endpoints still exist from GMS. DMS's scope excludes the
    /// finance track, so the seed simply does not create this row — no role can be
    /// granted it, and the endpoints stay gated rather than silently re-pointed at
    /// another menu.</summary>
    public const string Financials = "financials";
    /// <summary>Admin-triggered sends to other users. Everyone can always read and
    /// manage their own notifications without this.</summary>
    public const string Notifications = "notifications";

    // ── USER MANAGEMENT ──────────────────────────────────────────────────
    public const string Users = "users";
    public const string Roles = "roles";
    public const string AccountRequests = "account-requests";
    /// <summary>The Role Access screen itself — who may grant access to others.
    /// Replaces the old per-user "User Access" module grants.</summary>
    public const string RoleAccess = "role-access";
}
