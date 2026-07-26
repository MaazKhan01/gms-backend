using System;
using System.Security.Claims;
using Core.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Services;

// Reads the guest identity straight off the JWT claims — no DB lookup. The
// guest token carries a "guestId" claim (see VipAppService token generation).
public class CurrentGuest(IHttpContextAccessor _http) : ICurrentGuest
{
    public const string GuestIdClaim = "Id";

    private ClaimsPrincipal User => _http.HttpContext?.User;

    public int GuestId
        => int.TryParse(User?.FindFirstValue(GuestIdClaim), out var id) ? id : 0;

    public string Email => User?.FindFirstValue(ClaimTypes.Email);

    public bool IsAuthenticated => GuestId != 0;
}
