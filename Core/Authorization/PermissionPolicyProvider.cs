using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Core.Authorization;

/// <summary>
/// Permission codes live in the database, so the policy set cannot be enumerated
/// at startup the way reflecting over a constants class used to. This builds
/// "perm:{code}:{read|write}" policies on first use instead; anything else falls
/// through to the default provider.
/// </summary>
public class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options) { }

    public override async Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
    {
        var existing = await base.GetPolicyAsync(policyName);
        if (existing != null) return existing;

        if (!HasPermissionAttribute.TryParse(policyName, out string[] codes, out var level))
            return null;

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(codes, level))
            .Build();
    }
}
