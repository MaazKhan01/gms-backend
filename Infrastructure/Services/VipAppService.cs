using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.VipApp;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services;

// ============================================================================
// VIP Guest App service — mobile-facing logic (auth, events, itinerary, travel,
// sessions, preferences, profile). Support chat and notifications/devices have
// since been extracted to their own service/controller — see the remarks at
// the bottom of this file and of IVipAppService.
//
// `guestId` is the caller, resolved by the controller from ICurrentGuest (a
// Guests.UserId lookup off the token's User.Id plus role=="guest" — see CurrentGuest).
// Auth endpoints issue that token via OTP (guests have no password). Everything
// reads/writes existing GMS entities plus UserRefreshToken (shared with staff
// sessions — see GuestRefreshTokenType) and the Guest.PreferencesJson column.
// ============================================================================
public class VipAppService(
    IUnitOfWork _unitOfWork,
    IConfiguration _configuration,
    IEmailService _emailService,
    ITransportationConflictValidator _conflictValidator,
    IRideMirror _rideMirror,
    ILogger<VipAppService> _logger) : IVipAppService
{
    private const string OtpPurpose = "guest-login";
    private const string GuestRefreshTokenType = "guest-refresh";
    private static string NormalizeEmail(string email) => email?.Trim().ToLowerInvariant();


    private static DateTime? ToDt(DateOnly? d, string time = null)
    {
        if (d is null) return null;
        var t = TimeOnly.MinValue;
        if (!string.IsNullOrWhiteSpace(time) && TimeOnly.TryParse(time, out var parsed)) t = parsed;
        return d.Value.ToDateTime(t);
    }

    // "2h 15m" / "45m". Null when either end is missing or the span is negative.
    private static string FormatDuration(DateTime? from, DateTime? to)
    {
        if (from is null || to is null) return null;
        var mins = (int)Math.Round((to.Value - from.Value).TotalMinutes);
        if (mins < 0) return null;
        return mins < 60 ? $"{mins}m" : mins % 60 == 0 ? $"{mins / 60}h" : $"{mins / 60}h {mins % 60}m";
    }


    private async Task<List<int>> ResolveEventGuestIdsAsync(int guestId, Guid? eventId, CancellationToken ct)
    {
        var guest = await GetGuestAsync(guestId, ct);
        if (guest is null) return null;

        return await _unitOfWork.EventGuests.QueryNoTracking()
            .Where(eg => eg.GuestId == guest.Id)
            .Where(eg => eventId == null || eg.Event.PublicId == eventId.Value)
            .Select(eg => eg.Id)
            .ToListAsync(ct);
    }

    private Task<Guest> GetGuestAsync(int guestId, CancellationToken ct)
        => _unitOfWork.Guests.FindFirstOrDefaultAsync(g => g.Id == guestId, ct);

    // Refresh tokens carry the linked User.Id (see BuildRefreshToken), so the
    // refresh path resolves the person this way. Null for a User with no Guest
    // profile — which is how a staff refresh token gets rejected.
    private Task<Guest> GetGuestByUserIdAsync(int userId, CancellationToken ct)
        => _unitOfWork.Guests.FindFirstOrDefaultAsync(g => g.UserId == userId, ct);

    // The caller's participation in ONE event. Null when they aren't on it —
    // which is a 404/403 for the caller, never "fall back to another event".
    private Task<EventGuest> GetParticipationAsync(int guestId, Guid eventId, CancellationToken ct)
        => _unitOfWork.EventGuests.Query()
            .Include(eg => eg.Event)
            .Include(eg => eg.ServiceLevel)
            .FirstOrDefaultAsync(eg => eg.GuestId == guestId && eg.Event.PublicId == eventId, ct);

    // "May this person request a car?" — a display flag only. It is true if ANY
    // of the participations in scope allows it, because the itinerary can span
    // several events; the request itself is checked against the ONE participation
    // it names (see RequestTransportAsync), never against this.
    private async Task<bool> AllowsTransportAsync(List<int> eventGuestIds, CancellationToken ct)
    {
        var allowed = await _unitOfWork.EventGuests.QueryNoTracking()
            .Where(eg => eventGuestIds.Contains(eg.Id))
            .Select(eg => eg.AllowedServicesJson)
            .ToListAsync(ct);

        return allowed.Any(json => GuestServices.Allows(json, GuestServiceType.Transport));
    }

    // The person's most recent participation — the only sane source for the
    // profile screen's organisation/tier now that both are per event.
    private Task<EventGuest> LatestParticipationAsync(int guestId, CancellationToken ct)
        => _unitOfWork.EventGuests.QueryNoTracking()
            .Where(eg => eg.GuestId == guestId)
            // Same reason as GetParticipationAsync: the profile's tier now reads
            // through this navigation.
            .Include(eg => eg.ServiceLevel)
            .OrderByDescending(eg => eg.CreatedAt)
            .FirstOrDefaultAsync(ct);

    // ============================================================
    // Auth — OTP login issuing guest-scoped JWTs
    // ============================================================
    public async Task<ApiResponse<bool>> RequestOtpAsync(RequestOtpRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);

        // Don't leak whether the email is a known guest — always report success.
        var guest = await _unitOfWork.Guests.FindFirstOrDefaultAsync(g => g.Email == email, ct);
        if (guest is null)
            return ApiResponse<bool>.SuccessResponse(false, "Guest don't Exists");

        // Invalidate any outstanding codes for this email.
        var outstanding = await _unitOfWork.OtpVerifications
            .FindAsync(o => o.Email == email && o.Purpose == OtpPurpose && !o.IsUsed, ct);
        foreach (var o in outstanding) { o.IsUsed = true; o.UsedAt = DateTime.UtcNow; }

        // 4 digits, 1000-9999 so it never renders with a leading zero.
        var code = Random.Shared.Next(1000, 10000).ToString();
        await _unitOfWork.OtpVerifications.AddAsync(new OtpVerification
        {
            Email = email,
            OtpCode = code,
            Purpose = OtpPurpose,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        }, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _emailService.SendOtpEmailAsync(email, code, ct);
        return ApiResponse<bool>.SuccessResponse(true, "Otp Sent Successfully.");
    }

    public async Task<ApiResponse<GuestAuthResponse>> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);

        var otp = await _unitOfWork.OtpVerifications.Query()
            .Where(o => o.Email == email && o.OtpCode == request.Code
                     && o.Purpose == OtpPurpose && !o.IsUsed)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (otp is null)
            return ApiResponse<GuestAuthResponse>.UnauthorizedResponse("Invalid verification code");
        if (otp.ExpiresAt < DateTime.UtcNow)
            return ApiResponse<GuestAuthResponse>.UnauthorizedResponse("Verification code has expired. Request a new one.");

        var guest = await _unitOfWork.Guests.FindFirstOrDefaultAsync(g => g.Email == email, ct);
        if (guest is null)
            return ApiResponse<GuestAuthResponse>.NotFoundResponse("Guest not found");

        otp.IsUsed = true;
        otp.UsedAt = DateTime.UtcNow;

        var auth = await IssueTokensAsync(guest, ct);
        // A client that sent a device token registers it here; everyone else
        // just gets their most recent registration echoed back.
        var device = string.IsNullOrWhiteSpace(request.DeviceToken)
            ? await GetLatestDeviceAsync(guest.UserId, ct)
            : await UpsertDeviceAsync(guest.UserId, request, ct);
        auth.FcmToken = device?.Token;
        auth.DeviceId = device?.DeviceIdentifier;
        return ApiResponse<GuestAuthResponse>.SuccessResponse(auth, "Signed in");
    }

    private async Task<Device> UpsertDeviceAsync(int userId, VerifyOtpRequest request, CancellationToken ct)
    {
        try
        {
            return await DeviceRegistration.UpsertAsync(
                _unitOfWork, userId, request.DeviceToken,
                request.Platform, request.DeviceIdentifier, request.DeviceModel, ct: ct);
        }
        catch (Exception ex)
        {
            // Never fail a valid sign-in over push registration.
            _logger.LogError(ex, "Device registration during verify-otp failed for user {UserId}", userId);
            return null;
        }
    }

    // Read-only fallback — reports the guest's linked User's most recently
    // active registration when verify-otp carried no device token.
    private Task<Device> GetLatestDeviceAsync(int userId, CancellationToken ct)
        => _unitOfWork.Devices.Query()
            .Where(d => d.UserId == userId && d.IsActive)
            .OrderByDescending(d => d.LastActiveAt)
            .FirstOrDefaultAsync(ct);

    public async Task<ApiResponse<GuestAuthResponse>> RefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        try
        {
            var principal = ValidateJwt(refreshToken, validateLifetime: true);
            var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
            var userIdStr = principal.FindFirstValue("sub");

            if (string.IsNullOrEmpty(jti) || !int.TryParse(userIdStr, out var userId))
                return ApiResponse<GuestAuthResponse>.UnauthorizedResponse("Invalid refresh token");

            // Access tokens and staff refresh tokens are signed with the same key
            // and land in the same UserRefreshTokens table, so the token_type is
            // the only thing separating them — reject anything but a guest one
            // before its row is looked up.
            if (principal.FindFirstValue("token_type") != GuestRefreshTokenType)
                return ApiResponse<GuestAuthResponse>.UnauthorizedResponse("Invalid refresh token");

            var stored = await _unitOfWork.UserRefreshTokens.Query()
                .FirstOrDefaultAsync(t => t.Jti == jti && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow, ct);
            if (stored is null)
                return ApiResponse<GuestAuthResponse>.UnauthorizedResponse("Refresh token revoked or expired");

            // Belt and braces on top of the token_type check: the row must belong
            // to the User the token names, and that User must have a Guest profile.
            var guest = stored.UserId == userId ? await GetGuestByUserIdAsync(userId, ct) : null;
            if (guest is null)
                return ApiResponse<GuestAuthResponse>.UnauthorizedResponse("Guest not found");

            // No rotation — the refresh token stays valid for its whole
            // RefreshTokenExpirationDays window and only the access token is
            // reissued. Rotating made it single-use, which the mobile app can't
            // survive: two parallel 401s after a resume, or a response lost on a
            // flaky connection, left the client holding a token the server had
            // already revoked — an unrecoverable logout.
            // ponytail: no rotation means no stolen-token reuse detection either.
            // Revocation is logout + the UserRefreshTokens row.
            var jwt = _configuration.GetSection("Authentication:Jwt");
            var expiresAt = DateTime.UtcNow.AddMinutes(int.Parse(jwt["ExpirationMinutes"] ?? "60"));

            return ApiResponse<GuestAuthResponse>.SuccessResponse(new GuestAuthResponse
            {
                AccessToken = BuildAccessToken(guest, jwt, expiresAt),
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                Guest = MapProfile(guest)
            }, "Token refreshed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Guest refresh failed");
            return ApiResponse<GuestAuthResponse>.UnauthorizedResponse("Invalid refresh token");
        }
    }

    public async Task<ApiResponse<bool>> LogoutAsync(string refreshToken, CancellationToken ct)
    {
        try
        {
            var principal = ValidateJwt(refreshToken, validateLifetime: false);
            var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
            // Rows for staff sessions live in the same table, so without the
            // token_type check a guest logout could revoke a staff session.
            if (!string.IsNullOrEmpty(jti)
                && principal.FindFirstValue("token_type") == GuestRefreshTokenType)
            {
                var stored = await _unitOfWork.UserRefreshTokens.Query()
                    .FirstOrDefaultAsync(t => t.Jti == jti && !t.IsRevoked, ct);
                if (stored is not null)
                {
                    stored.IsRevoked = true;
                    _unitOfWork.UserRefreshTokens.Update(stored);
                    await _unitOfWork.SaveChangesAsync(ct);
                }
            }
        }
        catch { /* logout always succeeds from the client's view */ }
        return ApiResponse<bool>.SuccessResponse(true, "Logged out");
    }

    private async Task<GuestAuthResponse> IssueTokensAsync(Guest guest, CancellationToken ct)
    {
        var jwt = _configuration.GetSection("Authentication:Jwt");
        var expMinutes = int.Parse(jwt["ExpirationMinutes"] ?? "60");
        var expiresAt = DateTime.UtcNow.AddMinutes(expMinutes);
        var jti = Guid.NewGuid().ToString();

        var access = BuildAccessToken(guest, jwt, expiresAt);
        var refresh = BuildRefreshToken(guest, jwt, jti);

        // Guests are Users (Guest.UserId is a non-nullable 1:1), so their sessions
        // belong in the same table as staff ones — one revocation path, not two.
        await _unitOfWork.UserRefreshTokens.AddAsync(new UserRefreshToken
        {
            UserId = guest.UserId,
            Jti = jti,
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(jwt["RefreshTokenExpirationDays"] ?? "30")),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        }, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new GuestAuthResponse
        {
            AccessToken = access,
            RefreshToken = refresh,
            ExpiresAt = expiresAt,
            Guest = MapProfile(guest)
        };
    }

    private static string BuildAccessToken(Guest guest, IConfigurationSection jwt, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["JwtSecretKey"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var fullName = $"{guest.FirstName} {guest.LastName}".Trim();

        var claims = new List<Claim>
        {
            // The access token carries the guest's linked User.Id only (Guest is a
            // 1:1 profile extension of User — see the Guest entity remarks), never
            // Guest.Id. ICurrentUser, Clients.User(...) targeting and
            // AuditInterceptor's CreatedBy/UpdatedBy therefore resolve exactly as
            // they do for a staff token, and ICurrentGuest derives Guest.Id from
            // Guests.UserId. `sub` matches: the JWT handler maps inbound "sub" onto
            // ClaimTypes.NameIdentifier, so a differing value here would win the
            // FindFirst race and resolve the wrong identity.
            new("sub", guest.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, guest.UserId.ToString()),
            new(ClaimTypes.Email, guest.Email ?? string.Empty),
            new(ClaimTypes.Name, fullName),
            new("role", "guest"),
            new("email", guest.Email ?? string.Empty),
            new("fullName", fullName),
        };

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string BuildRefreshToken(Guest guest, IConfigurationSection jwt, string jti)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["JwtSecretKey"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            // User.Id, matching the access token — never Guest.Id. The refresh
            // path resolves the Guest from it via Guests.UserId, and a token whose
            // User has no Guest profile is rejected.
            new("sub", guest.UserId.ToString()),
            new(ClaimTypes.Email, guest.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, jti),
            new("token_type", GuestRefreshTokenType),
        };
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(int.Parse(jwt["RefreshTokenExpirationDays"] ?? "30")),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal ValidateJwt(string token, bool validateLifetime)
    {
        var jwt = _configuration.GetSection("Authentication:Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["JwtSecretKey"]));
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = validateLifetime,
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
        // MapInboundClaims off: the default map rewrites "sub" to
        // ClaimTypes.NameIdentifier, so callers reading the raw "sub" claim got null.
        return new JwtSecurityTokenHandler { MapInboundClaims = false }
            .ValidateToken(token, parameters, out _);
    }

    // ============================================================
    // Events / selection
    // ============================================================
    public async Task<ApiResponse<List<GuestEventResponse>>> GetEventsAsync(int guestId, CancellationToken ct)
    {
        var guest = await GetGuestAsync(guestId, ct);
        if (guest is null) return ApiResponse<List<GuestEventResponse>>.NotFoundResponse("Guest not found");

        // One person, one row here, one EventGuest per event they're on.
        var eventIds = await _unitOfWork.EventGuests.QueryNoTracking()
            .Where(eg => eg.GuestId == guest.Id)
            .Select(eg => eg.EventId).Distinct().ToListAsync(ct);

        var events = await _unitOfWork.Events.QueryNoTracking()
            .Where(e => eventIds.Contains(e.Id))
            .OrderByDescending(e => e.StartDate).ToListAsync(ct);

        var data = events.Select(e => new GuestEventResponse
        {
            Id = e.PublicId, Title = e.Title, Type = e.Type,
            StartDate = ToDt(e.StartDate), EndDate = ToDt(e.EndDate),
            ImageUrl = e.ImageUrl, Status = e.Status
        }).ToList();

        return ApiResponse<List<GuestEventResponse>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<List<GuestSessionResponse>>> GetEventSessionsAsync(int guestId, Guid eventId, CancellationToken ct)
    {
        var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
        if (ev is null) return ApiResponse<List<GuestSessionResponse>>.NotFoundResponse("Event not found");

        // The caller's participation in THIS event — their picks on another event
        // must not leak in, and not being on it is a 404 rather than an empty list.
        var participation = await GetParticipationAsync(guestId, eventId, ct);
        if (participation is null)
            return ApiResponse<List<GuestSessionResponse>>.NotFoundResponse("You are not registered for this event");

        var selected = await _unitOfWork.GuestSessions.QueryNoTracking()
            .Where(gs => gs.EventGuestId == participation.Id)
            .ToDictionaryAsync(gs => gs.SessionId, gs => gs.Status, ct);

        var sessions = await _unitOfWork.Sessions.QueryNoTracking()
            .Include(s => s.Event)
            .Where(s => s.EventId == ev.Id)
            .OrderBy(s => s.Date).ThenBy(s => s.Time).ToListAsync(ct);

        var data = sessions.Select(s => MapSession(s,
            selected.TryGetValue(s.Id, out var status) ? (status ?? "selected") : null)).ToList();
        return ApiResponse<List<GuestSessionResponse>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<bool>> SubmitSelectionAsync(int guestId, Guid eventId, SessionSelectionRequest request, CancellationToken ct)
    {
        var guest = await GetGuestAsync(guestId, ct);
        if (guest is null) return ApiResponse<bool>.NotFoundResponse("Guest not found");

        var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
        if (ev is null) return ApiResponse<bool>.NotFoundResponse("Event not found");

        var participation = await GetParticipationAsync(guestId, eventId, ct);
        if (participation is null)
            return ApiResponse<bool>.NotFoundResponse("You are not registered for this event");

        // Load the event's sessions with both keys so we can resolve the client's
        // public session guids to internal int ids.
        var eventSessions = await _unitOfWork.Sessions.QueryNoTracking()
            .Where(s => s.EventId == ev.Id)
            .Select(s => new { s.Id, s.PublicId }).ToListAsync(ct);
        var eventSessionIds = eventSessions.Select(s => s.Id).ToList();

        var existing = (await _unitOfWork.GuestSessions
            .FindAsync(gs => gs.EventGuestId == participation.Id && eventSessionIds.Contains(gs.SessionId), ct)).ToList();

        if (existing.Count > 0) _unitOfWork.GuestSessions.RemoveRange(existing);

        if (!request.Decline && request.SessionIds is { Count: > 0 })
        {
            var requested = request.SessionIds.ToHashSet();
            var toAdd = eventSessions.Where(s => requested.Contains(s.PublicId))
                .Select(s => new GuestSession { EventGuestId = participation.Id, SessionId = s.Id, Status = "selected" });
            await _unitOfWork.GuestSessions.AddRangeAsync(toAdd, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<bool>.SuccessResponse(true, request.Decline ? "Selection declined" : "Selection saved");
    }

    // ============================================================
    // Agenda — upcoming guest actions (flight / check-in / pickup)
    // ============================================================
    public async Task<ApiResponse<List<AgendaCardResponse>>> GetAgendaAsync(
        int guestId, Guid? eventId = null, CancellationToken ct = default)
    {
        // Same email can be a guest on several events — eventId narrows to one.
        var eventGuestIds = await ResolveEventGuestIdsAsync(guestId, eventId, ct);
        if (eventGuestIds is null) return ApiResponse<List<AgendaCardResponse>>.NotFoundResponse("Guest not found");

        var now = DateTime.UtcNow;
        var cards = new List<AgendaCardResponse>();

        var legs = await _unitOfWork.FlightLegs.QueryNoTracking()
            .Include(l => l.FromAirport).Include(l => l.ToAirport)
            .Where(l => eventGuestIds.Contains(l.Flight.EventGuestId) && l.StartTime > now).ToListAsync(ct);
        cards.AddRange(legs.Select(l => new AgendaCardResponse
        {
            Flag = "UPCOMING FLIGHT", Kind = "flight", RefId = l.PublicId,
            When = l.StartTime!.Value,
            Title = string.IsNullOrWhiteSpace(l.FlightNumber) ? "Flight" : $"Flight {l.FlightNumber}",
            Subtitle = $"{l.FromAirport?.Code} → {l.ToAirport?.Code}"
        }));

        var accs = await _unitOfWork.Accommodations.QueryNoTracking()
            .Include(a => a.Contract).ThenInclude(c => c.Hotel)
            .Where(a => eventGuestIds.Contains(a.EventGuestId) && a.CheckIn != null).ToListAsync(ct);
        cards.AddRange(accs
            .Select(a => new { a, when = ToDt(a.CheckIn) })
            .Where(x => x.when > now)
            .Select(x => new AgendaCardResponse
            {
                Flag = "UPCOMING CHECK-IN", Kind = "hotel", RefId = x.a.PublicId,
                When = x.when!.Value, Title = "Hotel Check-In",
                // Address alongside the name — the card is what the guest reads
                // on the way there, and the name alone doesn't say where.
                Subtitle = string.Join(" · ", new[] { x.a.Contract?.Hotel?.Name, x.a.Contract?.Hotel?.Address }
                    .Where(s => !string.IsNullOrWhiteSpace(s)))
            }));

        var trips = await _unitOfWork.Transports.QueryNoTracking()
            .Include(t => t.PickupLocation).Include(t => t.DropoffLocation)
            .Where(t => eventGuestIds.Contains(t.EventGuestId) && t.PickupTime > now).ToListAsync(ct);
        cards.AddRange(trips.Select(t => new AgendaCardResponse
        {
            Flag = "UPCOMING PICKUP", Kind = "transport", RefId = t.PublicId,
            When = t.PickupTime!.Value, Title = "Transport Pickup",
            Subtitle = $"{t.PickupLocation?.Address} → {t.DropoffLocation?.Address}"
        }));

        return ApiResponse<List<AgendaCardResponse>>.SuccessResponse(cards.OrderBy(c => c.When).ToList());
    }

    // ============================================================
    // Itinerary dates — which days this guest has anything on
    // ============================================================
    public async Task<ApiResponse<List<DateOnly>>> GetItineraryDatesAsync(
        int guestId, Guid? eventId = null, CancellationToken ct = default)
    {
        var eventGuestIds = await ResolveEventGuestIdsAsync(guestId, eventId, ct);
        if (eventGuestIds is null) return ApiResponse<List<DateOnly>>.NotFoundResponse("Guest not found");

        // Three narrow queries, each DISTINCT in SQL and pulling back dates only —
        // no entities, no includes. That's what keeps this cheap enough to call
        // on every calendar render.
        // .Date, not DateOnly.FromDateTime — it's the translation SQL Server has
        // always had (CAST AS date), so the DISTINCT happens server-side.
        var flightDates = await _unitOfWork.FlightLegs.QueryNoTracking()
            .Where(l => eventGuestIds.Contains(l.Flight.EventGuestId) && l.StartTime != null)
            .Select(l => l.StartTime.Value.Date)
            .Distinct()
            .ToListAsync(ct);

        var transportDates = await _unitOfWork.Transports.QueryNoTracking()
            .Where(t => eventGuestIds.Contains(t.EventGuestId) && t.PickupTime != null)
            .Select(t => t.PickupTime.Value.Date)
            .Distinct()
            .ToListAsync(ct);

        // A stay covers every night between the two ends, so this one has to be
        // expanded here — SQL has no cheap way to generate the range.
        var stays = await _unitOfWork.Accommodations.QueryNoTracking()
            .Where(a => eventGuestIds.Contains(a.EventGuestId) && a.CheckIn != null)
            .Select(a => new { From = a.CheckIn.Value, To = a.CheckOut })
            .ToListAsync(ct);

        var dates = new HashSet<DateOnly>(flightDates.Select(DateOnly.FromDateTime));
        dates.UnionWith(transportDates.Select(DateOnly.FromDateTime));
        foreach (var s in stays)
            for (var d = s.From; d <= (s.To ?? s.From); d = d.AddDays(1))
                dates.Add(d);

        return ApiResponse<List<DateOnly>>.SuccessResponse(dates.Order().ToList());
    }

    // ============================================================
    // Itinerary — flights / transport / accommodation / sessions in one call
    // ============================================================
    public async Task<ApiResponse<ItinerarySummaryResponse>> GetItineraryAsync(
        int guestId, Guid? eventId = null, DateOnly? date = null, CancellationToken ct = default)
    {
        var eventGuestIds = await ResolveEventGuestIdsAsync(guestId, eventId, ct);
        if (eventGuestIds is null) return ApiResponse<ItinerarySummaryResponse>.NotFoundResponse("Guest not found");

        // date == null → the whole itinerary. Otherwise only what falls on that
        // day; a hotel stay counts if the date lands anywhere inside it.
        var from = date?.ToDateTime(TimeOnly.MinValue);
        var to = from?.AddDays(1);

        var legs = await _unitOfWork.FlightLegs.QueryNoTracking()
            .Include(l => l.FromAirport).Include(l => l.ToAirport)
            .Include(l => l.Flight).ThenInclude(f => f.FlightClass)
            .Where(l => eventGuestIds.Contains(l.Flight.EventGuestId))
            .Where(l => date == null || (l.StartTime >= from && l.StartTime < to))
            .OrderBy(l => l.StartTime)
            .ToListAsync(ct);

        var trips = await _unitOfWork.Transports.QueryNoTracking()
            .Include(t => t.PickupLocation).Include(t => t.DropoffLocation)
            .Include(t => t.Vehicle).ThenInclude(v => v.VehicleType)
            .Include(t => t.Driver).ThenInclude(d => d.User)
            .Where(t => eventGuestIds.Contains(t.EventGuestId))
            .Where(t => date == null || (t.PickupTime >= from && t.PickupTime < to))
            .OrderBy(t => t.PickupTime == null).ThenBy(t => t.PickupTime)
            .ToListAsync(ct);

        var accs = await _unitOfWork.Accommodations.QueryNoTracking()
            .Include(a => a.Contract).ThenInclude(c => c.Hotel)
            .Include(a => a.RoomType)
            .Where(a => eventGuestIds.Contains(a.EventGuestId))
            // Inclusive of both ends — the guest is in the hotel on check-out day.
            .Where(a => date == null
                     || ((a.CheckIn == null || a.CheckIn <= date) && (a.CheckOut == null || a.CheckOut >= date)))
            .OrderBy(a => a.CheckIn)
            .ToListAsync(ct);

        // Grouped, not ToDictionary — two sibling guest rows could both point at
        // the same session, and a duplicate key would blow up the request.
        var picks = (await _unitOfWork.GuestSessions.QueryNoTracking()
            .Where(gs => eventGuestIds.Contains(gs.EventGuestId))
            .Select(gs => new { gs.SessionId, gs.Status })
            .ToListAsync(ct))
            .GroupBy(x => x.SessionId)
            .ToDictionary(g => g.Key, g => g.First().Status);

        var sessions = await _unitOfWork.Sessions.QueryNoTracking()
            .Include(s => s.Event)
            .Where(s => picks.Keys.Contains(s.Id))
            .Where(s => date == null || s.Date == date)
            .OrderBy(s => s.Date).ThenBy(s => s.Time)
            .ToListAsync(ct);

        var data = new ItinerarySummaryResponse
        {
            Flights = legs.Select(l => new FlightLegResponse
            {
                Id = l.PublicId,
                DepartureCode = l.FromAirport?.Code, DepartureAirport = l.FromAirport?.City,
                ArrivalCode = l.ToAirport?.Code, ArrivalAirport = l.ToAirport?.City,
                DateTime = l.StartTime, FlightNumber = l.FlightNumber,
                DepartureTime = l.Flight?.DepartureTime, ArrivalTime = l.Flight?.ArrivalTime,
                Class = l.Flight?.FlightClass?.Name, Status = l.Flight?.Status, Seat = l.Flight?.Seat,
                Duration = FormatDuration(l.StartTime ?? l.Flight?.DepartureTime, l.EndTime ?? l.Flight?.ArrivalTime)
            }).ToList(),

            // One entry per trip — the whole list is already here.
            Transports = trips.Select(MapTransport).ToList(),

            Accommodations = accs.Select(a => new AccommodationResponse
            {
                Id = a.PublicId,
                HotelName = a.Contract?.Hotel?.Name,
                HotelImageUrl = a.Contract?.Hotel?.ImageUrl,
                Address = a.Contract?.Hotel?.Address,
                CheckIn = ToDt(a.CheckIn),
                CheckOut = ToDt(a.CheckOut),
                RoomType = a.RoomType?.Name,
                ImageUrl = a.ImageUrl,
            }).ToList(),

            Sessions = sessions.Select(s => MapSession(s, picks[s.Id] ?? "selected")).ToList(),

            // Deliberately not filtered by `date` — it's a permission, not an
            // itinerary item.
            TransportAllowed = await AllowsTransportAsync(eventGuestIds, ct),
        };

        return ApiResponse<ItinerarySummaryResponse>.SuccessResponse(data);
    }

    // ============================================================
    // Travel
    // ============================================================
    // One entry per booking, legs nested inside it — a return booking is a single
    // entry with its outbound and inbound segments, not two unrelated rows.
    public async Task<ApiResponse<List<FlightBookingResponse>>> GetFlightsAsync(int guestId, Guid? eventId, CancellationToken ct)
    {
        var eventGuestIds = await ResolveEventGuestIdsAsync(guestId, eventId, ct);
        if (eventGuestIds is null) return ApiResponse<List<FlightBookingResponse>>.NotFoundResponse("Guest not found");

        var flights = await _unitOfWork.Flights.Query()
            .Include(f => f.FlightClass)
            .Include(f => f.Legs).ThenInclude(l => l.FromAirport)
            .Include(f => f.Legs).ThenInclude(l => l.ToAirport)
            .Where(f => eventGuestIds.Contains(f.EventGuestId)).ToListAsync(ct);

        var data = flights.Select(f =>
        {
            var legs = f.Legs.OrderBy(l => l.StartTime).ThenBy(l => l.Id).ToList();
            var first = legs.FirstOrDefault();
            var last = legs.LastOrDefault();

            return new FlightBookingResponse
            {
                Id = f.PublicId,
                FlightType = f.FlightType.ToString().ToLowerInvariant(),
                Class = f.FlightClass?.Name,
                Status = f.Status,
                Seat = f.Seat,
                DepartureTime = f.DepartureTime ?? first?.StartTime,
                ArrivalTime = f.ArrivalTime ?? last?.EndTime,
                Duration = FormatDuration(first?.StartTime ?? f.DepartureTime, last?.EndTime ?? f.ArrivalTime),
                ImageUrl = f.ImageUrl,
                Legs = legs.Select(l => new FlightLegResponse
                {
                    Id = l.PublicId,
                    DepartureCode = l.FromAirport?.Code, DepartureAirport = l.FromAirport?.City,
                    ArrivalCode = l.ToAirport?.Code, ArrivalAirport = l.ToAirport?.City,
                    DateTime = l.StartTime,
                    DepartureTime = l.StartTime ?? f.DepartureTime,
                    ArrivalTime = l.EndTime ?? f.ArrivalTime,
                    FlightNumber = l.FlightNumber,
                    Class = f.FlightClass?.Name,
                    Status = f.Status, Seat = f.Seat,
                    // Leg times first — they're the segment being shown. Booking-level
                    // depart/land times fill in when the leg has none.
                    Duration = FormatDuration(l.StartTime ?? f.DepartureTime, l.EndTime ?? f.ArrivalTime)
                }).ToList(),
            };
        }).OrderBy(x => x.DepartureTime).ToList();

        return ApiResponse<List<FlightBookingResponse>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<AccommodationResponse>> GetAccommodationAsync(int guestId, Guid? eventId, CancellationToken ct)
    {
        var eventGuestIds = await ResolveEventGuestIdsAsync(guestId, eventId, ct);
        if (eventGuestIds is null) return ApiResponse<AccommodationResponse>.NotFoundResponse("Guest not found");

        var acc = await _unitOfWork.Accommodations.Query()
            .Include(a => a.Contract).ThenInclude(c => c.Hotel)
            .Include(a => a.RoomType)
            .OrderBy(a => a.CheckIn)
            .FirstOrDefaultAsync(a => eventGuestIds.Contains(a.EventGuestId), ct);
        if (acc is null) return ApiResponse<AccommodationResponse>.NotFoundResponse("No accommodation found");

        var data = new AccommodationResponse
        {
            Id = acc.PublicId,
            HotelName = acc.Contract?.Hotel?.Name,
            HotelImageUrl = acc.Contract?.Hotel?.ImageUrl,
            Address = acc.Contract?.Hotel?.Address,
            CheckIn = ToDt(acc.CheckIn),
            CheckOut = ToDt(acc.CheckOut),
            RoomType = acc.RoomType?.Name,
            ImageUrl = acc.ImageUrl,
        };
        return ApiResponse<AccommodationResponse>.SuccessResponse(data);
    }

    public async Task<ApiResponse<TransportationResponse>> RequestTransportAsync(
        int guestId, TransportRequest request, CancellationToken ct)
    {
        try
        {
            if (request is null)
                return ApiResponse<TransportationResponse>.ErrorResponse("Request body is required");

            // eventId is required, not inferred. A person can be on several events
            // at once, and picking "the latest eligible one" would book a car
            // against the wrong event — wrong drivers, wrong dispatch board, wrong
            // permissions. If the client doesn't know which event, neither do we.
            if (request.EventId == Guid.Empty)
                return ApiResponse<TransportationResponse>.ErrorResponse("eventId is required");

            var guest = await GetGuestAsync(guestId, ct);
            if (guest is null) return ApiResponse<TransportationResponse>.NotFoundResponse("Guest not found");

            var participation = await GetParticipationAsync(guest.Id, request.EventId, ct);
            if (participation is null)
                return ApiResponse<TransportationResponse>.NotFoundResponse("You are not registered for this event");

            // Checked against THIS participation, not against "any event that
            // allows it": the itinerary's TransportAllowed flag only hides the
            // button, and self-service granted on one event says nothing about
            // another.
            if (!GuestServices.Allows(participation.AllowedServicesJson, GuestServiceType.Transport))
                return ApiResponse<TransportationResponse>.ForbiddenResponse(
                    "Transport requests are not enabled for you on this event");

            var pickupId = await ResolveLocationIdAsync(request.PickupLocationId, ct);
            if (pickupId is null) return ApiResponse<TransportationResponse>.ErrorResponse("Invalid pickup location");

            var dropoffId = await ResolveLocationIdAsync(request.DropoffLocationId, ct);
            if (dropoffId is null) return ApiResponse<TransportationResponse>.ErrorResponse("Invalid drop-off location");

            var transport = new Transport
            {
                EventGuestId = participation.Id,
                PickupLocationId = pickupId,
                DropoffLocationId = dropoffId,
                PickupTime = DateTime.Now,
                // No driver yet — "new" is the pool drivers accept from.
                TripStatus = TransportStatuses.New,
                // Mirrors "scheduled" set by TransportationScheduleService.
                RideSource = "on-demand",
            };

            await _unitOfWork.Transports.AddAsync(transport, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Into Firestore as status "new" — this is what puts the request in
            // front of the drivers. Best-effort: a Firestore outage still leaves
            // the row in SQL, where the driver app's /available-jobs also finds it.
            await _rideMirror.SyncAsync(transport.PublicId, ct);

            var created = await _unitOfWork.Transports.QueryNoTracking()
                .Include(t => t.PickupLocation).Include(t => t.DropoffLocation)
                .Include(t => t.Vehicle).ThenInclude(v => v.VehicleType)
                .FirstOrDefaultAsync(t => t.Id == transport.Id, ct);

            return ApiResponse<TransportationResponse>.SuccessResponse(
                MapTransport(created), "Transport requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transport request failed for guest {GuestId}", guestId);
            return ApiResponse<TransportationResponse>.ServerErrorResponse("Could not create the transport request");
        }
    }

    public async Task<ApiResponse<bool>> CancelTransportRequestAsync(
        int guestId, Guid transportId, CancellationToken ct)
    {
        try
        {
            var eventGuestIds = await ResolveEventGuestIdsAsync(guestId, null, ct);
            if (eventGuestIds is null) return ApiResponse<bool>.NotFoundResponse("Guest not found");

            // Cancellable only while nobody has taken it. One conditional UPDATE —
            // the "still new" test is part of the WHERE, so a driver accepting at
            // the same moment can't have the job cancelled out from under them:
            // whichever write lands second touches 0 rows (same shape as
            // TransportAppService.AcceptJobAsync). Guest-scoped in the same WHERE,
            // so one guest can never cancel another's ride.
            var cancelled = await _unitOfWork.Transports.Query()
                .Where(t => t.PublicId == transportId
                         && eventGuestIds.Contains(t.EventGuestId)
                         && t.TripStatus == TransportStatuses.New)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.TripStatus, TransportStatuses.Cancelled)
                    .SetProperty(t => t.UpdatedAt, DateTime.UtcNow), ct);

            if (cancelled > 0)
            {
                // Drops it out of the drivers' pool listener as well.
                await _rideMirror.SyncAsync(transportId, ct);
                return ApiResponse<bool>.SuccessResponse(true, "Transport request cancelled");
            }

            // 0 rows — separate "not yours/not there" from "too late", so the app
            // can tell the guest why the button did nothing.
            var status = await _unitOfWork.Transports.QueryNoTracking()
                .Where(t => t.PublicId == transportId && eventGuestIds.Contains(t.EventGuestId))
                .Select(t => t.TripStatus)
                .FirstOrDefaultAsync(ct);

            if (status is null)
                return ApiResponse<bool>.NotFoundResponse("Transport request not found");

            if (string.Equals(status, TransportStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
                return ApiResponse<bool>.SuccessResponse(true, "Transport request is already cancelled");

            return ApiResponse<bool>.ConflictResponse(
                "This request can no longer be cancelled — a driver has already been assigned to it");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cancelling transport request {TransportId} failed for guest {GuestId}", transportId, guestId);
            return ApiResponse<bool>.ServerErrorResponse("Could not cancel the transport request");
        }
    }

    private Task<int?> ResolveLocationIdAsync(Guid publicId, CancellationToken ct)
        => _unitOfWork.Locations.Query()
            .Where(l => l.PublicId == publicId)
            .Select(l => (int?)l.Id)
            .FirstOrDefaultAsync(ct);

    private static TransportationResponse MapTransport(Transport t) => new()
    {
        Id = t.PublicId,
        FromAddress = t.PickupLocation?.Address,
        ToAddress = t.DropoffLocation?.Address,
        FromLatitude = t.PickupLocation?.Latitude,
        FromLongitude = t.PickupLocation?.Longitude,
        ToLatitude = t.DropoffLocation?.Latitude,
        ToLongitude = t.DropoffLocation?.Longitude,
        PickupTime = t.PickupTime,
        // Guest app labels this "estimated arrival"; the planned drop-off time is
        // what that means now.
        EstimatedArrival = t.DropoffTime,
        // Kept alongside Vehicle below: the category on its own is what the older
        // guest-app screens read.
        VehicleType = t.Vehicle?.VehicleType?.Name,
        TripStatus = t.TripStatus,
        Vehicle = t.Vehicle is not { } veh ? null : new VehicleResponse
        {
            Id = veh.PublicId,
            Model = veh.VehicleModel,
            Number = veh.VehicleNumber,
            Type = veh.VehicleType?.Name,
            ImageUrl = veh.VehicleImage,
            Capacity = veh.Capacity,
        },
        Driver = t.Driver?.User is not { } drv ? null : new DriverResponse
        {
            Name = $"{drv.FirstName} {drv.LastName}".Trim(),
            Phone = drv.Phone,
            DriverUserId = drv.PublicId
        },
    };

    // Both transport lists read the same rows — one loader, so today/upcoming/
    // history can't disagree about what the guest is booked on. Null = no guest.
    private async Task<List<Transport>> LoadTransportsAsync(int guestId, Guid? eventId, CancellationToken ct)
    {
        var eventGuestIds = await ResolveEventGuestIdsAsync(guestId, eventId, ct);
        if (eventGuestIds is null) return null;

        return await _unitOfWork.Transports.Query()
            .Include(t => t.PickupLocation).Include(t => t.DropoffLocation)
            .Include(t => t.Vehicle).ThenInclude(v => v.VehicleType)
            .Include(t => t.Driver).ThenInclude(d => d.User)
            .Where(t => eventGuestIds.Contains(t.EventGuestId)).ToListAsync(ct);
    }

    public async Task<ApiResponse<List<TransportationResponse>>> GetTodayTransportationAsync(int guestId, Guid? eventId, CancellationToken ct)
    {
        var trips = await LoadTransportsAsync(guestId, eventId, ct);
        if (trips is null) return ApiResponse<List<TransportationResponse>>.NotFoundResponse("Guest not found");

        var today = DateTime.UtcNow.Date;
        // A finished ride is off the guest's day — the screen is "what's left
        // today", so completed jobs drop out as soon as the driver closes them.
        // Cancelled ones stay: the guest needs to see their ride was called off.
        var data = trips
            .Where(t => t.PickupTime?.Date == today)
            .Where(t => !string.Equals(t.TripStatus, TransportStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.PickupTime)
            .Select(MapTransport).ToList();

        // Empty list, not 404 — "nothing today" is a normal day for the app.
        return ApiResponse<List<TransportationResponse>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<TransportBookingsResponse>> GetTransportationBookingsAsync(int guestId, Guid? eventId, CancellationToken ct)
    {
        var trips = await LoadTransportsAsync(guestId, eventId, ct);
        if (trips is null) return ApiResponse<TransportBookingsResponse>.NotFoundResponse("Guest not found");

        var today = DateTime.UtcNow.Date;

        // Today's rides live on the other endpoint — skipped here so nothing shows
        // up twice. Of what's left: still-open and later = upcoming, the rest
        // (past dates, completed, cancelled) = history. No pickup time yet counts
        // as upcoming — it hasn't happened.
        // …except today's completed rides: /transportation/today drops those, so
        // without this they would fall out of the app entirely.
        var rest = trips
            .Where(t => t.PickupTime?.Date != today
                        || string.Equals(t.TripStatus, TransportStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            .ToList();
        bool IsUpcoming(Transport t) =>
            t.PickupTime is null
            || (t.PickupTime.Value.Date > today
                && TransportStatuses.Live.Contains(t.TripStatus, StringComparer.OrdinalIgnoreCase));

        var data = new TransportBookingsResponse
        {
            Upcoming = rest.Where(IsUpcoming).OrderBy(t => t.PickupTime).Select(MapTransport).ToList(),
            History = rest.Where(t => !IsUpcoming(t)).OrderByDescending(t => t.PickupTime).Select(MapTransport).ToList(),
        };
        return ApiResponse<TransportBookingsResponse>.SuccessResponse(data);
    }

    // ============================================================
    // Sessions
    // ============================================================
    public async Task<ApiResponse<List<GuestSessionResponse>>> GetSessionsAsync(
        int guestId, Guid? eventId = null, CancellationToken ct = default)
    {
        // Same email can be a guest on several events — eventId narrows to one.
        var eventGuestIds = await ResolveEventGuestIdsAsync(guestId, eventId, ct);
        if (eventGuestIds is null) return ApiResponse<List<GuestSessionResponse>>.NotFoundResponse("Guest not found");

        // Grouped rather than ToDictionaryAsync: several of the guest's rows could
        // in principle carry the same session, and a duplicate key would throw.
        var picks = (await _unitOfWork.GuestSessions.QueryNoTracking()
                .Where(gs => eventGuestIds.Contains(gs.EventGuestId))
                .Select(gs => new { gs.SessionId, gs.Status })
                .ToListAsync(ct))
            .GroupBy(gs => gs.SessionId)
            .ToDictionary(g => g.Key, g => g.First().Status);

        var sessions = await _unitOfWork.Sessions.QueryNoTracking()
            .Include(s => s.Event)
            .Where(s => picks.Keys.Contains(s.Id))
            .OrderBy(s => s.Date).ThenBy(s => s.Time).ToListAsync(ct);

        var data = sessions.Select(s => MapSession(s, picks[s.Id] ?? "selected")).ToList();
        return ApiResponse<List<GuestSessionResponse>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<SessionDetailResponse>> GetSessionDetailAsync(int guestId, Guid sessionId, CancellationToken ct)
    {
        var session = await _unitOfWork.Sessions.Query()
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.PublicId == sessionId, ct);
        if (session is null) return ApiResponse<SessionDetailResponse>.NotFoundResponse("Session not found");

        // The session belongs to one event, so the caller's participation in THAT
        // event is what carries their pick, their seat and their tier.
        var participation = await GetParticipationAsync(guestId, session.Event?.PublicId ?? Guid.Empty, ct);

        var pick = participation is null ? null : await _unitOfWork.GuestSessions
            .FindFirstOrDefaultAsync(gs => gs.EventGuestId == participation.Id && gs.SessionId == session.Id, ct);

        var assign = participation is null ? null : await _unitOfWork.SeatAssigns.Query()
            .Include(a => a.Seat).Include(a => a.Seating)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.EventGuestId == participation.Id && a.Seating.EventSessionId == session.Id, ct);

        var b = MapSession(session, pick?.Status ?? (pick != null ? "selected" : null));
        var data = new SessionDetailResponse
        {
            Id = b.Id, EventId = b.EventId, Title = b.Title, Category = b.Category,
            Start = b.Start, End = b.End, VenueName = b.VenueName, SelectionStatus = b.SelectionStatus,
            Status = pick != null ? "Confirmed" : "Pending",
            Seating = assign is null ? null : new SeatingResponse
            {
                Category = participation?.ServiceLevel?.Code,
                Block = assign.Seat?.Block,
                Row = assign.Seat?.SeatInfo,
                Seat = assign.Seat?.Code,
                Gate = assign.Seat?.Gate
            }
        };
        return ApiResponse<SessionDetailResponse>.SuccessResponse(data);
    }

    private static GuestSessionResponse MapSession(Session s, string selectionStatus) => new()
    {
        Id = s.PublicId, EventId = s.Event?.PublicId ?? Guid.Empty, Title = s.Title,
        Start = ToDt(s.Date, s.Time), VenueName = s.VenueName,
        SelectionStatus = selectionStatus
    };

    // ============================================================
    // Preferences — stored as JSON on Guest.PreferencesJson
    // ============================================================
    public async Task<ApiResponse<GuestPreferencesResponse>> GetPreferencesAsync(int guestId, CancellationToken ct)
    {
        var guest = await GetGuestAsync(guestId, ct);
        if (guest is null) return ApiResponse<GuestPreferencesResponse>.NotFoundResponse("Guest not found");

        var prefs = string.IsNullOrWhiteSpace(guest.PreferencesJson)
            ? new GuestPreferencesResponse()
            : JsonSerializer.Deserialize<GuestPreferencesResponse>(guest.PreferencesJson) ?? new GuestPreferencesResponse();
        return ApiResponse<GuestPreferencesResponse>.SuccessResponse(prefs);
    }

    public async Task<ApiResponse<GuestPreferencesResponse>> UpdatePreferencesAsync(int guestId, GuestPreferencesResponse request, CancellationToken ct)
    {
        var guest = await GetGuestAsync(guestId, ct);
        if (guest is null) return ApiResponse<GuestPreferencesResponse>.NotFoundResponse("Guest not found");

        guest.PreferencesJson = JsonSerializer.Serialize(request);
        _unitOfWork.Guests.Update(guest);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<GuestPreferencesResponse>.SuccessResponse(request, "Preferences saved");
    }

    // ============================================================
    // Profile
    // ============================================================
    public async Task<ApiResponse<GuestProfileResponse>> GetProfileAsync(int guestId, CancellationToken ct)
    {
        var guest = await GetGuestAsync(guestId, ct);
        if (guest is null) return ApiResponse<GuestProfileResponse>.NotFoundResponse("Guest not found");
        return ApiResponse<GuestProfileResponse>.SuccessResponse(
            MapProfile(guest, await LatestParticipationAsync(guest.Id, ct)));
    }

    public async Task<ApiResponse<GuestProfileResponse>> UpdateProfileAsync(int guestId, UpdateProfileRequest request, CancellationToken ct)
    {
        var guest = await GetGuestAsync(guestId, ct);
        if (guest is null) return ApiResponse<GuestProfileResponse>.NotFoundResponse("Guest not found");

        guest.FirstName = request.FirstName ?? guest.FirstName;
        guest.LastName = request.LastName ?? guest.LastName;
        _unitOfWork.Guests.Update(guest);

        // Organisation is per event, so it is written to the participation the
        // profile screen is showing (the most recent one) rather than to the
        // person — editing it must not rewrite every event's affiliation.
        var latest = await LatestParticipationAsync(guest.Id, ct);
        if (request.Organization != null && latest != null)
        {
            var tracked = await _unitOfWork.EventGuests.Query().FirstOrDefaultAsync(eg => eg.Id == latest.Id, ct);
            if (tracked != null)
            {
                tracked.Organization = request.Organization;
                tracked.OrganizationId = null;
                _unitOfWork.EventGuests.Update(tracked);
                latest = tracked;
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<GuestProfileResponse>.SuccessResponse(MapProfile(guest, latest), "Profile updated");
    }

    public async Task<ApiResponse<bool>> UpdateSettingsAsync(int guestId, UpdateSettingsRequest request, CancellationToken ct)
    {
        var guest = await GetGuestAsync(guestId, ct);
        if (guest is null) return ApiResponse<bool>.NotFoundResponse("Guest not found");

        guest.NotificationsEnabled = request.NotificationsEnabled;
        guest.Language = request.Language ?? guest.Language;
        // Location is derived from the event, not stored on Guest — ignored here.
        _unitOfWork.Guests.Update(guest);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<bool>.SuccessResponse(true, "Settings updated");
    }

    // One mapper for every profile payload — verify-otp, refresh, GET and PUT
    // /profile — so a field added here reaches all of them at once.
    // `latest` is the person's most recent participation and may be null (a
    // person with no event yet) — organisation and tier are per event, so those
    // two come from it while everything else is person-level.
    private static GuestProfileResponse MapProfile(Guest g, EventGuest latest = null) => new()
    {
        Id = g.PublicId, FirstName = g.FirstName, LastName = g.LastName, Email = g.Email,
        Organization = latest?.Organization, Tier = latest?.ServiceLevel?.Code, PhotoUrl = g.PhotoUrl
    };

    // Support chat lives entirely on SupportChatService / SupportChatController now.

    // Notifications / devices live entirely on NotificationService / NotificationsController
    // now (see GetGuestNotificationsAsync et al.) — same move as support chat above.
}
