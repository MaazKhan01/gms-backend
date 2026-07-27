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
// Core.Constants.GuestClaims.GuestId claim plus role=="guest" — see CurrentGuest).
// Auth endpoints issue that token via OTP (guests have no password). Everything
// reads/writes existing GMS entities plus GuestRefreshToken and the
// Guest.PreferencesJson column.
// ============================================================================
public class VipAppService(
    IUnitOfWork _unitOfWork,
    IConfiguration _configuration,
    IEmailService _emailService,
    ILogger<VipAppService> _logger) : IVipAppService
{
    private const string OtpPurpose = "guest-login";


    private static DateTime? ToDt(DateOnly? d, string time = null)
    {
        if (d is null) return null;
        var t = TimeOnly.MinValue;
        if (!string.IsNullOrWhiteSpace(time) && TimeOnly.TryParse(time, out var parsed)) t = parsed;
        return d.Value.ToDateTime(t);
    }

    private Task<Guest> GetGuestAsync(int guestId, CancellationToken ct)
        => _unitOfWork.Guests.FindFirstOrDefaultAsync(g => g.Id == guestId, ct);

    // ============================================================
    // Auth — OTP login issuing guest-scoped JWTs
    // ============================================================
    public async Task<ApiResponse<bool>> RequestOtpAsync(RequestOtpRequest request, CancellationToken ct)
    {
        // Don't leak whether the email is a known guest — always report success.
        var guest = await _unitOfWork.Guests.FindFirstOrDefaultAsync(g => g.Email == request.Email, ct);
        if (guest is null)
            return ApiResponse<bool>.SuccessResponse(false, "Guest don't Exists");

        // Invalidate any outstanding codes for this email.
        var outstanding = await _unitOfWork.OtpVerifications
            .FindAsync(o => o.Email == request.Email && o.Purpose == OtpPurpose && !o.IsUsed, ct);
        foreach (var o in outstanding) { o.IsUsed = true; o.UsedAt = DateTime.UtcNow; }

        var code = Random.Shared.Next(100000, 999999).ToString();
        await _unitOfWork.OtpVerifications.AddAsync(new OtpVerification
        {
            Email = request.Email,
            OtpCode = code,
            Purpose = OtpPurpose,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        }, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _emailService.SendOtpEmailAsync(request.Email, code, ct);
        return ApiResponse<bool>.SuccessResponse(true, "Otp Sent Successfully.");
    }

    public async Task<ApiResponse<GuestAuthResponse>> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct)
    {
        var otp = await _unitOfWork.OtpVerifications.Query()
            .Where(o => o.Email == request.Email && o.OtpCode == request.Code
                     && o.Purpose == OtpPurpose && !o.IsUsed)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (otp is null)
            return ApiResponse<GuestAuthResponse>.UnauthorizedResponse("Invalid verification code");
        if (otp.ExpiresAt < DateTime.UtcNow)
            return ApiResponse<GuestAuthResponse>.UnauthorizedResponse("Verification code has expired. Request a new one.");

        var guest = await _unitOfWork.Guests.FindFirstOrDefaultAsync(g => g.Email == request.Email, ct);
        if (guest is null)
            return ApiResponse<GuestAuthResponse>.NotFoundResponse("Guest not found");

        otp.IsUsed = true;
        otp.UsedAt = DateTime.UtcNow;

        var auth = await IssueTokensAsync(guest, ct);
        return ApiResponse<GuestAuthResponse>.SuccessResponse(auth, "Signed in");
    }

    public async Task<ApiResponse<GuestAuthResponse>> RefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        try
        {
            var principal = ValidateJwt(refreshToken, validateLifetime: true);
            var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
            var guestIdStr = principal.FindFirstValue("sub");

            if (string.IsNullOrEmpty(jti) || !int.TryParse(guestIdStr, out var guestId))
                return ApiResponse<GuestAuthResponse>.UnauthorizedResponse("Invalid refresh token");

            var stored = await _unitOfWork.GuestRefreshTokens.Query()
                .FirstOrDefaultAsync(t => t.Jti == jti && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow, ct);
            if (stored is null)
                return ApiResponse<GuestAuthResponse>.UnauthorizedResponse("Refresh token revoked or expired");

            var guest = await GetGuestAsync(guestId, ct);
            if (guest is null)
                return ApiResponse<GuestAuthResponse>.UnauthorizedResponse("Guest not found");

            stored.IsRevoked = true;
            _unitOfWork.GuestRefreshTokens.Update(stored);

            var auth = await IssueTokensAsync(guest, ct);
            return ApiResponse<GuestAuthResponse>.SuccessResponse(auth, "Token refreshed");
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
            if (!string.IsNullOrEmpty(jti))
            {
                var stored = await _unitOfWork.GuestRefreshTokens.Query()
                    .FirstOrDefaultAsync(t => t.Jti == jti && !t.IsRevoked, ct);
                if (stored is not null)
                {
                    stored.IsRevoked = true;
                    _unitOfWork.GuestRefreshTokens.Update(stored);
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

        await _unitOfWork.GuestRefreshTokens.AddAsync(new GuestRefreshToken
        {
            GuestId = guest.Id,
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
            new("sub", guest.Id.ToString()),
            new(ClaimTypes.NameIdentifier, guest.Id.ToString()),
            new(ClaimTypes.Email, guest.Email ?? string.Empty),
            new(ClaimTypes.Name, fullName),
            new("role", "guest"),
            new(CurrentGuest.GuestIdClaim, guest.Id.ToString()), // ICurrentGuest reads this
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
            new("sub", guest.Id.ToString()),
            new(ClaimTypes.Email, guest.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, jti),
            new("token_type", "refresh"),
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
        return new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
    }

    // ============================================================
    // Events / selection
    // ============================================================
    public async Task<ApiResponse<List<GuestEventResponse>>> GetEventsAsync(int guestId, CancellationToken ct)
    {
        var guest = await GetGuestAsync(guestId, ct);
        if (guest is null) return ApiResponse<List<GuestEventResponse>>.NotFoundResponse("Guest not found");

        // A person can be a guest across several events (one Guest row per event,
        // same email). Surface all of them.
        var eventIds = await _unitOfWork.Guests.QueryNoTracking()
            .Where(g => g.Email == guest.Email)
            .Select(g => g.EventId).Distinct().ToListAsync(ct);

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

        var selected = await _unitOfWork.GuestSessions.QueryNoTracking()
            .Where(gs => gs.GuestId == guestId)
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

        // Load the event's sessions with both keys so we can resolve the client's
        // public session guids to internal int ids.
        var eventSessions = await _unitOfWork.Sessions.QueryNoTracking()
            .Where(s => s.EventId == ev.Id)
            .Select(s => new { s.Id, s.PublicId }).ToListAsync(ct);
        var eventSessionIds = eventSessions.Select(s => s.Id).ToList();

        var existing = (await _unitOfWork.GuestSessions
            .FindAsync(gs => gs.GuestId == guestId && eventSessionIds.Contains(gs.SessionId), ct)).ToList();

        if (existing.Count > 0) _unitOfWork.GuestSessions.RemoveRange(existing);

        if (!request.Decline && request.SessionIds is { Count: > 0 })
        {
            var requested = request.SessionIds.ToHashSet();
            var toAdd = eventSessions.Where(s => requested.Contains(s.PublicId))
                .Select(s => new GuestSession { GuestId = guestId, SessionId = s.Id, Status = "selected" });
            await _unitOfWork.GuestSessions.AddRangeAsync(toAdd, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<bool>.SuccessResponse(true, request.Decline ? "Selection declined" : "Selection saved");
    }

    // ============================================================
    // Agenda — upcoming guest actions (flight / check-in / pickup)
    // ============================================================
    public async Task<ApiResponse<List<AgendaCardResponse>>> GetAgendaAsync(int guestId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var cards = new List<AgendaCardResponse>();

        var legs = await _unitOfWork.FlightLegs.QueryNoTracking()
            .Include(l => l.FromAirport).Include(l => l.ToAirport)
            .Where(l => l.Flight.GuestId == guestId && l.StartTime > now).ToListAsync(ct);
        cards.AddRange(legs.Select(l => new AgendaCardResponse
        {
            Flag = "UPCOMING FLIGHT", Kind = "flight", RefId = l.PublicId,
            When = l.StartTime!.Value,
            Title = string.IsNullOrWhiteSpace(l.FlightNumber) ? "Flight" : $"Flight {l.FlightNumber}",
            Subtitle = $"{l.FromAirport?.Code} → {l.ToAirport?.Code}"
        }));

        var accs = await _unitOfWork.Accommodations.QueryNoTracking()
            .Include(a => a.Hotel).Where(a => a.GuestId == guestId && a.CheckIn != null).ToListAsync(ct);
        cards.AddRange(accs
            .Select(a => new { a, when = ToDt(a.CheckIn) })
            .Where(x => x.when > now)
            .Select(x => new AgendaCardResponse
            {
                Flag = "UPCOMING CHECK-IN", Kind = "hotel", RefId = x.a.PublicId,
                When = x.when!.Value, Title = "Hotel Check-In", Subtitle = x.a.Hotel?.Name
            }));

        var trips = await _unitOfWork.Transports.QueryNoTracking()
            .Include(t => t.PickupLocation).Include(t => t.DropoffLocation)
            .Where(t => t.GuestId == guestId && t.PickupTime > now).ToListAsync(ct);
        cards.AddRange(trips.Select(t => new AgendaCardResponse
        {
            Flag = "UPCOMING PICKUP", Kind = "transport", RefId = t.PublicId,
            When = t.PickupTime!.Value, Title = "Transport Pickup",
            Subtitle = $"{t.PickupLocation?.Address} → {t.DropoffLocation?.Address}"
        }));

        return ApiResponse<List<AgendaCardResponse>>.SuccessResponse(cards.OrderBy(c => c.When).ToList());
    }

    // ============================================================
    // Travel
    // ============================================================
    public async Task<ApiResponse<List<FlightLegResponse>>> GetFlightsAsync(int guestId, CancellationToken ct)
    {
        var flights = await _unitOfWork.Flights.Query()
            .Include(f => f.FlightClass)
            .Include(f => f.Legs).ThenInclude(l => l.FromAirport)
            .Include(f => f.Legs).ThenInclude(l => l.ToAirport)
            .Where(f => f.GuestId == guestId).ToListAsync(ct);

        var data = flights.SelectMany(f => f.Legs.Select(l => new FlightLegResponse
        {
            Id = l.PublicId,
            DepartureCode = l.FromAirport?.Code, DepartureAirport = l.FromAirport?.City,
            ArrivalCode = l.ToAirport?.Code, ArrivalAirport = l.ToAirport?.City,
            DateTime = l.StartTime, FlightNumber = l.FlightNumber,
            Class = f.FlightClass?.Name, Status = f.Status, Seat = f.Seat
        })).OrderBy(x => x.DateTime).ToList();

        return ApiResponse<List<FlightLegResponse>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<AccommodationResponse>> GetAccommodationAsync(int guestId, CancellationToken ct)
    {
        var acc = await _unitOfWork.Accommodations.Query()
            .Include(a => a.Hotel).Include(a => a.RoomType)
            .OrderBy(a => a.CheckIn)
            .FirstOrDefaultAsync(a => a.GuestId == guestId, ct);
        if (acc is null) return ApiResponse<AccommodationResponse>.NotFoundResponse("No accommodation found");

        var data = new AccommodationResponse
        {
            Id = acc.PublicId,
            HotelName = acc.Hotel?.Name,
            Address = acc.Hotel?.Address,
            CheckIn = ToDt(acc.CheckIn),
            CheckOut = ToDt(acc.CheckOut),
            RoomType = acc.RoomType?.Name,
        };
        return ApiResponse<AccommodationResponse>.SuccessResponse(data);
    }

    public async Task<ApiResponse<TransportationResponse>> GetTransportationAsync(int guestId, CancellationToken ct)
    {
        var trips = await _unitOfWork.Transports.Query()
            .Include(t => t.PickupLocation).Include(t => t.DropoffLocation).Include(t => t.VehicleType)
            .Include(t => t.Driver).ThenInclude(d => d.User)
            .Where(t => t.GuestId == guestId).ToListAsync(ct);

        var primary = trips.FirstOrDefault();
        if (primary is null) return ApiResponse<TransportationResponse>.NotFoundResponse("No transportation found");

        var data = new TransportationResponse
        {
            Id = primary.PublicId,
            FromAddress = primary.PickupLocation?.Address,
            ToAddress = primary.DropoffLocation?.Address,
            PickupTime = primary.PickupTime,
            EstimatedArrival = primary.EstimatedArrival,
            VehicleType = primary.VehicleType?.Name,
            TripStatus = primary.TripStatus,
            Driver = primary.Driver?.User is not { } drv ? null : new DriverResponse
            {
                Name = $"{drv.FirstName} {drv.LastName}".Trim(), Role = "Chauffeur",
                Phone = drv.Phone
            },
            OtherJourneys = trips.Skip(1).Select(t => new JourneyResponse
            {
                Id = t.PublicId, Label = $"{t.PickupLocation?.Address} → {t.DropoffLocation?.Address}"
            }).ToList()
        };
        return ApiResponse<TransportationResponse>.SuccessResponse(data);
    }

    // ============================================================
    // Sessions
    // ============================================================
    public async Task<ApiResponse<List<GuestSessionResponse>>> GetSessionsAsync(int guestId, CancellationToken ct)
    {
        var picks = await _unitOfWork.GuestSessions.QueryNoTracking()
            .Where(gs => gs.GuestId == guestId)
            .ToDictionaryAsync(gs => gs.SessionId, gs => gs.Status, ct);

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

        var pick = await _unitOfWork.GuestSessions
            .FindFirstOrDefaultAsync(gs => gs.GuestId == guestId && gs.SessionId == session.Id, ct);

        var assign = await _unitOfWork.SeatAssigns.Query()
            .Include(a => a.Seat).Include(a => a.Seating)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.GuestId == guestId && a.Seating.EventSessionId == session.Id, ct);

        var guest = await GetGuestAsync(guestId, ct);

        var b = MapSession(session, pick?.Status ?? (pick != null ? "selected" : null));
        var data = new SessionDetailResponse
        {
            Id = b.Id, EventId = b.EventId, Title = b.Title, Category = b.Category,
            Start = b.Start, End = b.End, VenueName = b.VenueName, SelectionStatus = b.SelectionStatus,
            Status = pick != null ? "Confirmed" : "Pending",
            Seating = assign is null ? null : new SeatingResponse
            {
                Category = guest?.Tier,
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
        return ApiResponse<GuestProfileResponse>.SuccessResponse(MapProfile(guest));
    }

    public async Task<ApiResponse<GuestProfileResponse>> UpdateProfileAsync(int guestId, UpdateProfileRequest request, CancellationToken ct)
    {
        var guest = await GetGuestAsync(guestId, ct);
        if (guest is null) return ApiResponse<GuestProfileResponse>.NotFoundResponse("Guest not found");

        guest.FirstName = request.FirstName ?? guest.FirstName;
        guest.LastName = request.LastName ?? guest.LastName;
        guest.Organization = request.Organization ?? guest.Organization;
        _unitOfWork.Guests.Update(guest);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<GuestProfileResponse>.SuccessResponse(MapProfile(guest), "Profile updated");
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

    private static GuestProfileResponse MapProfile(Guest g) => new()
    {
        Id = g.PublicId, FirstName = g.FirstName, LastName = g.LastName, Email = g.Email,
        Organization = g.Organization, Tier = g.Tier
    };

    // Support chat lives entirely on SupportChatService / SupportChatController now.

    // Notifications / devices live entirely on NotificationService / NotificationsController
    // now (see GetGuestNotificationsAsync et al.) — same move as support chat above.
}
