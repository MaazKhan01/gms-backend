using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Authorization;

/// <summary>
/// The read/write rule itself, kept free of ASP.NET types so the endpoint
/// handler, ICurrentUser and any in-service check all answer identically.
/// </summary>
public static class AccessEvaluator
{
    public static bool Allows(
        IEnumerable<string> readCodes,
        IEnumerable<string> writeCodes,
        string permissionCode,
        AccessLevel level)
    {
        if (string.IsNullOrWhiteSpace(permissionCode)) return false;

        var writes = writeCodes as ICollection<string> ?? writeCodes?.ToList() ?? new List<string>();

        // Write is never implied by Read — that is the whole point of the two flags.
        if (writes.Contains(permissionCode, StringComparer.OrdinalIgnoreCase))
            return true;
        if (level == AccessLevel.Write)
            return false;

        // Write does imply Read: a role that may edit a page may obviously open it.
        var reads = readCodes as ICollection<string> ?? readCodes?.ToList() ?? new List<string>();
        return reads.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }
}
