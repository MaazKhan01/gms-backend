using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace Core.Authorization;

/// <summary>
/// The single place a read/write decision is made. Reads the `read` / `write`
/// claims minted from RolePermissions — no per-controller permission logic
/// anywhere.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User == null) return Task.CompletedTask;

        var reads = context.User.FindAll(AccessClaims.Read).Select(c => c.Value).ToList();
        var writes = context.User.FindAll(AccessClaims.Write).Select(c => c.Value).ToList();

        // Any one of the requirement's codes is enough — see HasPermissionAttribute.
        if (requirement.PermissionCodes.Any(code =>
                AccessEvaluator.Allows(reads, writes, code, requirement.Level)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
