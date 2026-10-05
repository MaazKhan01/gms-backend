namespace Core.Constants;

public static class GuestTypes
{
    public const string Dignitary    = "dignitary";
    public const string Delegate     = "delegate";
    public const string Media        = "media";
    public const string Staff        = "staff";
    public const string VIP          = "vip";
    public const string Observer     = "observer";
}

public static class GuestInvitationStatus
{
    public const string NotSent   = "not_sent";
    public const string Sent      = "sent";
    public const string Opened    = "opened";
    public const string Accepted  = "accepted";
    public const string Declined  = "declined";
}

public static class GuestAccreditationStatus
{
    public const string NotIssued = "not_issued";
    public const string Issued    = "issued";
    public const string Revoked   = "revoked";
}

/// <summary>Visa document validity on a delegate's participation. These are
/// validity states, not application-workflow states — a delegate who needs no
/// visa is handled by waiving the readiness item, not by a status here.
/// Kept in sync with the literal default in ApplicationDBContext.Gms.cs.</summary>
public static class VisaStatuses
{
    public const string Pending       = "pending";
    public const string Active        = "active";
    public const string ExpiringSoon  = "expiring-soon";
}

/// <summary>Travel-insurance document validity on a delegate's participation.</summary>
public static class InsuranceStatuses
{
    public const string Active        = "active";
    public const string ExpiringSoon  = "expiring-soon";
}