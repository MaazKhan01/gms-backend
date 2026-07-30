using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Core.Interfaces.Repositories;

namespace Core.Helpers
{
    public class RealTimeHubService(IUnitOfWork _unitOfWork) : Hub
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

                // A guest token carries only its linked User.Id (the NameIdentifier
                // above) plus role=="guest". Senders still target guests by Guest.Id
                // ("guest:{Guest.Id}" — see TransportAppService), so resolve it from
                // Guests.UserId here. The role check mirrors CurrentGuest's guard — a
                // staff token must never join a guest group.
                var role = Context.User.FindFirst("role")?.Value
                    ?? Context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                var isGuestToken = string.Equals(role, "guest", StringComparison.OrdinalIgnoreCase);
                if (isGuestToken && int.TryParse(userId, out var guestUserId))
                {
                    var guestId = await _unitOfWork.Guests.QueryNoTracking()
                        .Where(g => g.UserId == guestUserId)
                        .Select(g => g.Id)
                        .FirstOrDefaultAsync();
                    if (guestId != 0)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"guest:{guestId}");
                    }
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
