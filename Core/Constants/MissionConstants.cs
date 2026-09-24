using System;

namespace Core.Constants;

// DMS mission-domain option sets. These are STATUS values with code behaviour
// attached (gates, transitions, readiness), not admin-managed lookups — a value
// nobody wrote a code path for would silently do nothing. Admin-editable
// reference data (departments, mission types) lives in its own table instead.

// Mission roles are NOT here: a delegate's role on a mission is a Roles row
// flagged IsDelegateRole (EventGuest.MissionRoleId), so the list is admin-managed
// and the same row carries the portal access that role should come with.

public static class DestinationTiers
{
    public const string Regional        = "regional";
    public const string InternationalA  = "international_a";
    public const string InternationalB  = "international_b";
    public const string InternationalC  = "international_c";

    public static readonly string[] All = { Regional, InternationalA, InternationalB, InternationalC };
    public static bool IsValid(string value) => Array.IndexOf(All, value) >= 0;
}

public static class FundingModels
{
    public const string OrgPaid = "org_paid";
    public const string Hosted  = "hosted";
    public const string Mixed   = "mixed";

    public static readonly string[] All = { OrgPaid, Hosted, Mixed };
    public static bool IsValid(string value) => Array.IndexOf(All, value) >= 0;
}

/// <summary>Lifecycle of an invitation received from a host organisation.</summary>
/// <summary>HR's sign-off on a nomination. Everything starts pending; rejected
/// sends it back to the coordinator with <c>HrVerificationNote</c> saying why.</summary>
public static class HrVerificationStatuses
{
    public const string Pending  = "pending";
    public const string Verified = "verified";
    public const string Rejected = "rejected";

    public static bool IsValid(string value) =>
        value == Pending || value == Verified || value == Rejected;
}

/// <summary>Role codes the mission logic reasons about. The delegate-role list
/// itself is data (Roles flagged IsDelegateRole) — only the few with behaviour
/// attached are named here, and they must match the seeded Roles.Code.</summary>
public static class DelegateRoleCodes
{
    public const string HeadOfDelegation = "head-of-delegation";
}

public static class HostInvitationStatuses
{
    public const string Logged    = "logged";
    public const string Converted = "converted";
    public const string Declined  = "declined";

    public static bool IsValid(string value) =>
        value == Logged || value == Converted || value == Declined;
}

/// <summary>Nomination-letter track. `changes_requested` returns to `sent` on
/// re-issue, which produces a new version rather than editing the old one.</summary>
public static class NominationLetterStatuses
{
    public const string NotGenerated     = "not_generated";
    public const string Draft            = "draft";
    public const string Sent             = "sent";
    public const string ChangesRequested = "changes_requested";
    public const string Acknowledged     = "acknowledged";
}

/// <summary>The readiness checklist. Each item is derived from real booking data;
/// a red one blocks Travel Ready unless waived with a reason.</summary>
public static class ReadinessItems
{
    public const string Passport      = "passport";
    public const string Visa          = "visa";
    public const string Flight        = "flight";
    public const string Accommodation = "accommodation";
    public const string Transport     = "transport";

    public static readonly string[] All = { Passport, Visa, Flight, Accommodation, Transport };
}

public static class IncidentCategories
{
    public const string Late    = "late";
    public const string Lost    = "lost";
    public const string Medical = "medical";
    public const string Other   = "other";

    public static readonly string[] All = { Late, Lost, Medical, Other };
    public static bool IsValid(string value) => Array.IndexOf(All, value) >= 0;
}

public static class IncidentSeverities
{
    public const string Low    = "low";
    public const string Medium = "medium";
    public const string High   = "high";

    public static readonly string[] All = { Low, Medium, High };
    public static bool IsValid(string value) => Array.IndexOf(All, value) >= 0;
}

public static class IncidentStatuses
{
    public const string Open       = "open";
    public const string InProgress = "in_progress";
    public const string Resolved   = "resolved";
    public const string Escalated  = "escalated";

    public static readonly string[] All = { Open, InProgress, Resolved, Escalated };
    public static bool IsValid(string value) => Array.IndexOf(All, value) >= 0;

    /// <summary>Only resolved closes an incident — escalated is still open, just
    /// somebody else's problem now.</summary>
    public static bool IsClosed(string value) => value == Resolved;
}

/// <summary>Whether the delegate raised it themselves or an officer logged it for
/// them — materially different when the mission is reviewed afterwards.</summary>
public static class IncidentChannels
{
    public const string App    = "app";
    public const string Portal = "portal";

    public static readonly string[] All = { App, Portal };
    public static bool IsValid(string value) => Array.IndexOf(All, value) >= 0;
}

public static class PostMissionReportStatuses
{
    public const string NotSubmitted = "not_submitted";
    public const string Submitted    = "submitted";
    public const string Approved     = "approved";
}

public static class CombinedReportStatuses
{
    public const string Draft     = "draft";
    public const string InReview  = "in_review";
    public const string Approved  = "approved";
    public const string Published = "published";
}
