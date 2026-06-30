using System.Collections.Generic;
using Core.Common;

namespace Core.Constants;

/// <summary>
/// Canonical list of all GMS modules that can be granted as cross-module read access.
/// Key = slug stored in UserModuleGrant.Module.
/// </summary>
public static class ModuleDefinitions
{
    public record ModuleInfo(string Slug, string DisplayName, string ViewPermission);

    public static readonly IReadOnlyList<ModuleInfo> All = new[]
    {
        new ModuleInfo("events",        "Events",         PermissionCodes.EventsView),
        new ModuleInfo("guests",        "Guests",         PermissionCodes.GuestsView),
        new ModuleInfo("invitations",   "Invitations",    PermissionCodes.InvitationsView),
        new ModuleInfo("travel",        "Travel & Logistics", PermissionCodes.TravelView),
        new ModuleInfo("accreditation", "Accreditation",  PermissionCodes.AccreditationView),
        new ModuleInfo("venue",         "Venue Config",   PermissionCodes.VenueView),
        new ModuleInfo("seating",       "Seating",        PermissionCodes.SeatingView),
        new ModuleInfo("meetings",      "Meetings",       PermissionCodes.MeetingsView),
        new ModuleInfo("protocol",      "Protocol",       PermissionCodes.ProtocolView),
        new ModuleInfo("financials",    "Financials",     PermissionCodes.FinancialsView),
        new ModuleInfo("reports",       "Reports",        PermissionCodes.ReportsView),
        new ModuleInfo("dashboard",     "Dashboard",      PermissionCodes.DashboardView),
    };

    // Slug → ViewPermission for fast JWT enrichment.
    public static readonly IReadOnlyDictionary<string, string> ViewPermissionBySlug =
        new Dictionary<string, string>(
            System.Linq.Enumerable.ToDictionary(All, m => m.Slug, m => m.ViewPermission));
}
