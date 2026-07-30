using System;
using System.Linq;
using System.Security.Claims;
using Core.Common.Interfaces;
using Core.Interfaces.Repositories;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Services;

// Resolves the calling guest from the guest JWT. The token carries only the
// guest's linked User.Id (ClaimTypes.NameIdentifier) plus role=="guest" — no
// Guest.Id claim — so Guest.Id is looked up from Guests.UserId (1:1, see the
// Guest entity remarks). One query per request, cached.
//
// The role=="guest" guard matters: NameIdentifier is a plain Users.Id on staff
// tokens too, and without the guard a staff token whose user happens to own a
// Guest row would resolve as that guest.
public class CurrentGuest(IHttpContextAccessor _http, IUnitOfWork _unitOfWork) : ICurrentGuest
{
    private const string GuestRoleValue = "guest";

    private int? _guestId;

    private ClaimsPrincipal User => _http.HttpContext?.User;

    // Both spellings: the token is issued with a short "role" claim, and whether
    // it survives as "role" or is mapped to ClaimTypes.Role depends on the
    // handler's MapInboundClaims (JsonWebTokenHandler vs JwtSecurityTokenHandler).
    private bool IsGuestToken =>
        string.Equals(
            User?.FindFirstValue("role") ?? User?.FindFirstValue(ClaimTypes.Role),
            GuestRoleValue,
            StringComparison.OrdinalIgnoreCase);

    public int GuestId => _guestId ??= ResolveGuestId();

    private int ResolveGuestId()
    {
        if (!IsGuestToken)
        {
            return 0;
        }

        if (!int.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId == 0)
        {
            return 0;
        }

        return _unitOfWork.Guests.QueryNoTracking()
            .Where(g => g.UserId == userId)
            .Select(g => g.Id)
            .FirstOrDefault();
    }

    public string Email => User?.FindFirstValue(ClaimTypes.Email);

    public bool IsAuthenticated => GuestId != 0;
}
