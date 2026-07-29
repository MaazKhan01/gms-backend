namespace Core.Constants;

/// <summary>
/// Which client an API call comes from. Sent on every request as the
/// <see cref="Header"/> header, and stamped into the access token as the
/// "client" claim at login so later requests can be gated on it.
/// </summary>
public static class ClientApps
{
    public const string Header = "X-Client-App";

    public const string Portal = "portal";
    public const string DriverApp = "driver-app";
    public const string GuestApp = "guest-app";

    /// <summary>Normalises the header value. A missing/unknown value is treated
    /// as the portal — that's the client that existed before this header did.</summary>
    public static string Normalize(string value)
        => string.Equals(value?.Trim(), DriverApp, System.StringComparison.OrdinalIgnoreCase)
            ? DriverApp
            : Portal;

    public static bool IsPortal(string value) => Normalize(value) == Portal;
}
