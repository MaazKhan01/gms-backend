namespace Core.Constants;

/// <summary>
/// How an event organises its guests — chosen when the event is created and
/// changeable afterwards.
/// <para>
/// <b>Fixed</b> uses the Service Level catalogue: every guest sits on a level,
/// the level's bundled services and rules (capacity, required guest fields)
/// apply, and one person may appear once per level.
/// </para>
/// <para>
/// <b>Flexible</b> is the pre-service-level flow: no levels, no capacity or
/// required-field enforcement, and <c>Guest.Tier</c> is a plain string again.
/// One person appears at most once per event.
/// </para>
/// Stored on <c>Events.GuestModel</c> as one of these lowercase codes.
/// </summary>
public static class EventGuestModels
{
    public const string Fixed = "fixed";
    public const string Flexible = "flexible";

    public static readonly string[] All = { Fixed, Flexible };

    /// <summary>
    /// Flexible is the default: it is the behaviour the product had before
    /// service levels existed, so an event that never states a preference keeps
    /// working without restrictions rather than suddenly enforcing rules.
    /// </summary>
    public const string Default = Flexible;

    /// <summary>
    /// Maps whatever the client sent onto a known code, falling back to the
    /// default. Null/blank is treated as "not stated" rather than invalid so
    /// older clients that don't know about this field keep working.
    /// </summary>
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Default;
        var v = value.Trim().ToLowerInvariant();
        return v == Fixed ? Fixed : v == Flexible ? Flexible : Default;
    }

    public static bool IsValid(string value)
        => !string.IsNullOrWhiteSpace(value)
           && (value.Trim().ToLowerInvariant() is Fixed or Flexible);

    /// <summary>True when the event enforces the Service Level flow.</summary>
    public static bool UsesServiceLevels(string value)
        => Normalize(value) == Fixed;
}
