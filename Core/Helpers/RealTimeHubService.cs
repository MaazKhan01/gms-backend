using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core.Helpers
{
    public class RealTimeHubService : Hub
    {
        public async Task JoinGroup(string connectionId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, connectionId).ConfigureAwait(false);
        }

        public async Task LeaveGroup(string connectionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, connectionId).ConfigureAwait(false);
        }

        public override async Task OnConnectedAsync()
        {
            // Join group based on UserId from token
            if (Context.User?.Identity?.IsAuthenticated == true)
            {
                var userId = Context.UserIdentifier ?? Context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, userId);
                }

                // Guest tokens carry a "guestId" claim (see CurrentGuest.GuestIdClaim) and
                // also set NameIdentifier to the guest's own internal id — which can numerically
                // collide with a User's internal id above. Give guests a distinctly-named group
                // so guest-targeted sends (Groups, not Clients.User) can't cross-deliver to a User.
                var guestId = Context.User.FindFirst("guestId")?.Value;
                if (!string.IsNullOrEmpty(guestId))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"guest:{guestId}");
                }
            }

            // Join group based on custom connectionId from query string (if provided by client)
            var httpContext = Context.GetHttpContext();
            var customConnectionId = httpContext?.Request.Query["connectionId"].ToString();
            if (!string.IsNullOrEmpty(customConnectionId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, customConnectionId);
            }

            await base.OnConnectedAsync();
        }
    }
}
