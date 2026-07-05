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
