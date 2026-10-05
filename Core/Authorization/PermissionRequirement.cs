using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace Core.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>Any one of these at <see cref="Level"/> admits the caller.</summary>
    public IReadOnlyList<string> PermissionCodes { get; }
    public string PermissionCode => PermissionCodes.FirstOrDefault();
    public AccessLevel Level { get; }

    public PermissionRequirement(string permissionCode, AccessLevel level)
        : this(new[] { permissionCode }, level) { }

    public PermissionRequirement(IReadOnlyList<string> permissionCodes, AccessLevel level)
    {
        PermissionCodes = permissionCodes;
        Level = level;
    }
}
