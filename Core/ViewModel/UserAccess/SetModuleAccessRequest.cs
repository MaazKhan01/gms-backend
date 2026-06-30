using System.Collections.Generic;

namespace Core.ViewModel.UserAccess;

public class SetModuleAccessRequest
{
    /// <summary>
    /// Complete list of module slugs that should be granted to the user.
    /// Anything not in this list will be revoked. Natively-included modules
    /// are ignored (no-op) — they cannot be revoked here.
    /// </summary>
    public List<string> GrantedModules { get; set; } = new();
}
