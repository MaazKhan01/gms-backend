namespace Core.Authorization;

/// <summary>
/// Claim types carrying the signed-in user's role access. One claim per
/// permission code per level, written by AuthService when the access token is
/// issued.
/// </summary>
public static class AccessClaims
{
    /// <summary>Permission codes this user may read. Value = Permission.Code.</summary>
    public const string Read = "read";

    /// <summary>Permission codes this user may write. Value = Permission.Code.</summary>
    public const string Write = "write";

    public static string For(AccessLevel level) =>
        level == AccessLevel.Write ? Write : Read;
}
