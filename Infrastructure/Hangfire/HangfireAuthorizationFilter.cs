using Hangfire.Dashboard;
using Core.Constants;

namespace Infrastructure.Hangfire;

// Gates /hangfire — job payloads/history can include PII, so require an
// authenticated admin in every environment (not just production).
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext.User;

        return user?.Identity?.IsAuthenticated == true
            && user.FindFirst("roleCode")?.Value == Roles.ADMIN;
    }
}
