using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Constants;

/// <summary>
/// The three services that are NOT dynamic. Flight, Accommodation and Transport
/// keep their relational tables (Flights / Accommodations / Transports), their
/// hand-written forms and their conflict rules, because the VIP app, the driver
/// app, dispatch, the dashboard and inventory counting all read those tables by
/// foreign key — the exact risk left open in docs/service-levels-v2.md §8.
///
/// They still exist as <c>Service</c> rows so a ServiceLevel can include them and
/// the guest checklist can sequence them, but their data never goes into
/// GuestServiceEntry.ValuesJson: it is written through ITravelService, and the
/// guest plan reads the relational rows back. Every OTHER service is dynamic.
///
/// A service is recognised by its <c>Code</c>, so the catalogue refuses to rename
/// or delete these three — see ServiceCatalogService.
/// </summary>
public static class SystemServices
{
    public const string Flight = "flight";
    public const string Accommodation = "accommodation";
    public const string Transport = "transport";

    public static readonly IReadOnlyList<string> All = new[] { Flight, Accommodation, Transport };

    public static bool IsSystem(string code)
        => !string.IsNullOrWhiteSpace(code)
           && All.Contains(code.Trim().ToLowerInvariant());
}
