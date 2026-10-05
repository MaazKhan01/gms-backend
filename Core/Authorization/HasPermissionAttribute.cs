using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace Core.Authorization;

/// <summary>
/// The one authorization gate for protected endpoints: this action needs Read
/// (default) or Write on the named permission, per the caller's role. Permission
/// codes are database rows (Permissions.Code) — <see cref="PermissionPolicyProvider"/>
/// materialises the policy on demand, so a new menu needs no code change.
/// </summary>
/// <remarks>
/// More than one code may be given, in which case ANY of them lets the caller
/// through. That is for the handful of endpoints a shared read genuinely serves
/// several menus from — the delegate roster is read by Delegates, Accreditation,
/// Seating and Meetings — and it is not a licence to widen a gate: two stacked
/// attributes mean AND, so a single attribute is the only way to say OR.
/// </remarks>
public class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "perm:";

    public HasPermissionAttribute(string permissionCode, AccessLevel level = AccessLevel.Read)
        : this(level, permissionCode) { }

    /// <summary>Any one of <paramref name="permissionCodes"/> at <paramref name="level"/> admits the caller.</summary>
    public HasPermissionAttribute(AccessLevel level, params string[] permissionCodes)
    {
        PermissionCodes = permissionCodes ?? new string[0];
        Level = level;
        Policy = PolicyNameFor(PermissionCodes, level);
    }

    public string[] PermissionCodes { get; }
    public string PermissionCode => PermissionCodes.FirstOrDefault();
    public AccessLevel Level { get; }

    public static string PolicyNameFor(string permissionCode, AccessLevel level)
        => PolicyNameFor(new[] { permissionCode }, level);

    public static string PolicyNameFor(IEnumerable<string> permissionCodes, AccessLevel level)
        => $"{PolicyPrefix}{string.Join("|", permissionCodes)}:{(level == AccessLevel.Write ? "write" : "read")}";

    /// <summary>
    /// Parses a policy name back out, yielding every code of an any-of policy.
    /// Returns false for anything that is not one of ours.
    /// </summary>
    public static bool TryParse(string policyName, out string[] permissionCodes, out AccessLevel level)
    {
        permissionCodes = null;
        level = AccessLevel.Read;
        if (string.IsNullOrEmpty(policyName) || !policyName.StartsWith(PolicyPrefix))
            return false;

        var rest = policyName.Substring(PolicyPrefix.Length);
        var sep = rest.LastIndexOf(':');
        if (sep <= 0 || sep == rest.Length - 1) return false;

        var codes = rest.Substring(0, sep).Split('|');
        if (codes.Any(string.IsNullOrWhiteSpace)) return false;

        var levelPart = rest.Substring(sep + 1);
        if (levelPart == "write") level = AccessLevel.Write;
        else if (levelPart != "read") return false;

        permissionCodes = codes;
        return true;
    }
}
