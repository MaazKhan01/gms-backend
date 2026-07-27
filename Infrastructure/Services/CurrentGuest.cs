using System;
using System.Security.Claims;
using Core.Common.Interfaces;
using Core.Constants;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Services;

// Reads the guest identity straight off the JWT claims — no DB lookup. The
// guest token carries a GuestClaims.GuestId ("Id") claim (see VipAppService
// token generation).
//
// Also requires the "role" claim to be "guest": a Guest and a User can share
// the same internal id, so trusting the id claim alone would let a User token
// resolve as "Guest #{their own user id}" if that claim were ever present on
// a User token too (see AuthService — it deliberately never adds it).
public class CurrentGuest(IHttpContextAccessor _http) : ICurrentGuest
{
    public const string GuestIdClaim = GuestClaims.GuestId;
    private const string GuestRoleValue = "guest";

    private ClaimsPrincipal User => _http.HttpContext?.User;

    private string? Role =>
     User?.FindFirstValue(ClaimTypes.Role);

    private bool isGuest =>
        string.Equals(
            Role,
            GuestRoleValue,
            StringComparison.OrdinalIgnoreCase);
    public int GuestId => GetGuestId();

    private int GetGuestId()
    {
        if (!isGuest)
        {
            return 0;
        }

        var guestIdClaim = User?.FindFirstValue(GuestIdClaim);

        if (string.IsNullOrWhiteSpace(guestIdClaim))
        {
            return 0;
        }

        return int.TryParse(guestIdClaim, out var guestId)
            ? guestId
            : 0;
    }
    public string Email => User?.FindFirstValue(ClaimTypes.Email);

    public bool IsAuthenticated => GuestId != 0;
}
