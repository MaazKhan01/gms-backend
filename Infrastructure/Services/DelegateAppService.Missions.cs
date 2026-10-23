using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.Constants;
using Core.ViewModel.Common;
using Core.ViewModel.DelegateApp;
using Core.ViewModel.Noification;
using Core.ViewModel.Reports;
using Core.ViewModel.Travel;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

// ============================================================================
// Delegate App — mission screens (DMS-Delegate prototype): mission card / home,
// visa, accreditation, stays, change requests, places, help requests, mission
// journal, post-mission report. Everything is read from (or written to) a single
// participation — the caller's EventGuest on the eventId they name — never
// inferred across missions.
// ============================================================================
public partial class DelegateAppService
{
    private const string VisaServiceCode = "visa";

    private static readonly string[] MissionSortOrder =
    {
        DelegateMissionStatuses.Live, DelegateMissionStatuses.Upcoming, DelegateMissionStatuses.Nominated,
        DelegateMissionStatuses.Completed, DelegateMissionStatuses.Cancelled,
    };

    private static readonly HashSet<string> TitleStopWords = new(StringComparer.OrdinalIgnoreCase) { "of", "the", "and", "&", "for", "in" };

    // ── Mission card ───────────────────────────────────────────────────────

    /// <summary>"Swim Fest 2027" → "SF-2027", "GCC Youth Aquatics 2026" → "GYA-2026".
    /// ponytail: derived from the title because Events has no code column; add
    /// Event.Code when a coordinator needs to set one that doesn't follow the title.</summary>
    internal static string MissionCode(string title, DateOnly? start)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var year = words.FirstOrDefault(w => w.Length == 4 && w.All(char.IsDigit)) ?? start?.Year.ToString();
        var initials = new string(words
            .Where(w => char.IsLetter(w[0]) && !TitleStopWords.Contains(w))
            .Select(w => char.ToUpperInvariant(w[0])).ToArray());
        if (initials.Length == 0) return year;
        return year is null ? initials : $"{initials}-{year}";
    }

    /// <summary>The delegate's view of a mission. Cancelled beats everything;
    /// a nomination HR hasn't verified is "nominated" whatever the dates say.</summary>
    internal static string DelegateMissionStatus(Event e, string hrStatus, DateOnly today)
    {
        if (e.Status == MissionStatuses.Cancelled) return DelegateMissionStatuses.Cancelled;
        if (e.Status is MissionStatuses.Completed or MissionStatuses.Closed || (e.EndDate is { } end && end < today))
            return DelegateMissionStatuses.Completed;
        if (hrStatus != HrVerificationStatuses.Verified) return DelegateMissionStatuses.Nominated;
        if (e.StartDate is { } start && start <= today) return DelegateMissionStatuses.Live;
        return DelegateMissionStatuses.Upcoming;
    }

    private static GuestEventResponse MapMission(EventGuest eg)
    {
        var e = eg.Event;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var status = DelegateMissionStatus(e, eg.HrVerificationStatus, today);
        int? total = e.StartDate is { } s && e.EndDate is { } en ? en.DayNumber - s.DayNumber + 1 : null;

        return new GuestEventResponse
        {
            Id = e.PublicId, Title = e.Title, Type = e.Type,
            StartDate = ToDt(e.StartDate), EndDate = ToDt(e.EndDate),
            ImageUrl = e.ImageUrl, Status = e.Status,
            Code = MissionCode(e.Title, e.StartDate),
            VenueName = e.VenueName,
            Destination = e.Destination?.Address,
            MissionStatus = status,
            CurrentDay = status == DelegateMissionStatuses.Live && e.StartDate is { } st
                ? today.DayNumber - st.DayNumber + 1 : null,
            TotalDays = total,
            MissionRole = eg.MissionRole?.Name,
            Subgroup = eg.Subgroup,
            HrVerificationStatus = eg.HrVerificationStatus,
        };
    }

    // Everything the mission screens read off one participation.
    private Task<EventGuest> LoadMissionParticipationAsync(int guestId, Guid eventId, CancellationToken ct)
        => _unitOfWork.EventGuests.QueryNoTracking()
            .Include(eg => eg.Event).ThenInclude(e => e.Destination)
            .Include(eg => eg.Event).ThenInclude(e => e.Coordinator).ThenInclude(u => u.Role)
            .Include(eg => eg.MissionRole)
            .Include(eg => eg.ServiceLevel)
            .Include(eg => eg.Guest).ThenInclude(g => g.User)
            .Include(eg => eg.NominatedByUser).ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(eg => eg.GuestId == guestId && eg.Event.PublicId == eventId, ct);

    private static ApiResponse<T> NotOnMission<T>() => ApiResponse<T>.NotFoundResponse("You are not on this mission");

    // Same envelope, different payload type — for passing another service's failure through.
    private static ApiResponse<T> Fail<T, TFrom>(ApiResponse<TFrom> from) => new()
    {
        Success = false, Message = from.Message, Errors = from.Errors,
        ErrorCode = from.ErrorCode, StatusCode = from.StatusCode,
    };

    private static string FullName(User u) => u is null ? null : $"{u.FirstName} {u.LastName}".Trim();

    // The mission's coordinator, else whoever nominated them.
    private static User CoordinatorOf(EventGuest p) => p.Event.Coordinator ?? p.NominatedByUser;

    // ── Home ───────────────────────────────────────────────────────────────

    public async Task<ApiResponse<MissionHomeResponse>> GetMissionHomeAsync(int guestId, Guid eventId, CancellationToken ct)
    {
        var p = await LoadMissionParticipationAsync(guestId, eventId, ct);
        if (p is null) return NotOnMission<MissionHomeResponse>();

        var mission = MapMission(p);
        var readiness = await _readiness.GetForParticipationAsync(p.Id, ct);
        var agenda = await GetAgendaAsync(guestId, eventId, ct);
        var report = await _unitOfWork.PostMissionReports.QueryNoTracking()
            .FirstOrDefaultAsync(r => r.EventGuestId == p.Id, ct);
        var coordinator = CoordinatorOf(p);

        var data = new MissionHomeResponse
        {
            Mission = mission,
            Coordinator = coordinator is null ? null : new ContactResponse
            {
                Name = FullName(coordinator), Role = coordinator.Role?.Name, Phone = coordinator.Phone,
            },
            NextUp = agenda.Data?.FirstOrDefault(),
            Visa = await BuildVisaAsync(p, ct),
            Checklist = readiness?.Items.Select(i => new ReadinessCheckResponse
            {
                Key = i.Key, Status = i.Status, Detail = i.Detail,
            }).ToList() ?? new(),
            TravelReady = readiness?.TravelReady ?? false,
            Journal = await JournalSummaryAsync(p, ct),
            ReportStatus = report?.Status ?? PostMissionReportStatuses.NotSubmitted,
            UnreadNotifications = await _unitOfWork.Notifications.QueryNoTracking()
                .CountAsync(n => n.UserId == p.Guest.UserId && n.Read != true, ct),
            Allowance = new AllowanceResponse
            {
                Status = p.PerDiemStatus, Amount = p.PerDiemAmount, Currency = p.PerDiemCurrency,
                SettlementAmount = p.SettlementAmount, SettlementPaidOn = p.SettlementPaidOn,
                FundingModel = p.Event.FundingModel,
            },
        };

        switch (mission.MissionStatus)
        {
            case DelegateMissionStatuses.Nominated:
                data.NominationSteps = await NominationStepsAsync(p, ct);
                break;
            case DelegateMissionStatuses.Cancelled:
                data.Cancellation = new CancellationResponse
                {
                    Reason = p.Event.CancellationReason,
                    CancelledAt = p.Event.CancelledAt,
                    Bookings = await BookingsOfAsync(p, ct),
                };
                break;
            case DelegateMissionStatuses.Completed:
                data.Summary = new MissionSummaryResponse
                {
                    DaysAttended = mission.TotalDays,
                    Results = SplitLines(report?.KeyOutcomes),
                    ReportSubmittedOn = report?.SubmittedOn,
                    ReportApprovedOn = report?.Status == PostMissionReportStatuses.Approved ? report.UpdatedAt : null,
                    ReportApprovedBy = report?.Status == PostMissionReportStatuses.Approved ? await UserNameAsync(report.UpdatedBy, ct) : null,
                    JournalEntries = data.Journal.Total,
                };
                break;
        }

        return ApiResponse<MissionHomeResponse>.SuccessResponse(data);
    }

    private static List<string> SplitLines(string text) => string.IsNullOrWhiteSpace(text)
        ? new()
        : text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private async Task<string> UserNameAsync(int? userId, CancellationToken ct)
        => userId is null ? null : await _unitOfWork.Users.QueryNoTracking()
            .Where(u => u.Id == userId).Select(u => (u.FirstName + " " + u.LastName).Trim()).FirstOrDefaultAsync(ct);

    // Nominated → HR verified → host acknowledged the letter → invite / travel open.
    // The first step not done is the current one.
    private async Task<List<StepResponse>> NominationStepsAsync(EventGuest p, CancellationToken ct)
    {
        var letter = await _unitOfWork.NominationLetters.QueryNoTracking()
            .Where(l => l.EventId == p.EventId)
            .Select(l => new { l.Status, l.RespondedOn })
            .FirstOrDefaultAsync(ct);
        var invite = await _unitOfWork.Invitations.QueryNoTracking()
            .Where(i => i.EventGuestId == p.Id && i.SentAt != null)
            .OrderBy(i => i.SentAt).Select(i => i.SentAt).FirstOrDefaultAsync(ct);

        var nominator = p.NominatedByUser;
        var steps = new List<StepResponse>
        {
            new() { Key = "nominated", Done = true, Date = p.NominatedOn ?? p.CreatedAt,
                    Detail = nominator is null ? null : string.Join(" · ", new[] { FullName(nominator), nominator.Role?.Name }.Where(x => !string.IsNullOrWhiteSpace(x))) },
            new() { Key = "hr_verification", Done = p.HrVerificationStatus == HrVerificationStatuses.Verified,
                    Date = p.HrVerifiedOn, Detail = p.HrVerificationStatus == HrVerificationStatuses.Rejected ? p.HrVerificationNote : null },
            new() { Key = "host_confirmation", Done = letter?.Status == NominationLetterStatuses.Acknowledged, Date = letter?.RespondedOn },
            new() { Key = "invite_travel", Done = invite != null, Date = invite },
        };
        var current = steps.FirstOrDefault(s => !s.Done);
        if (current != null) current.Current = true;
        return steps;
    }

    // What was booked for the mission and where each booking stands — the
    // cancelled mission's "what was undone" list.
    private async Task<List<UndoneItemResponse>> BookingsOfAsync(EventGuest p, CancellationToken ct)
    {
        var items = new List<UndoneItemResponse>();

        var flights = await _unitOfWork.Flights.QueryNoTracking()
            .Where(f => f.EventGuestId == p.Id)
            .Select(f => new
            {
                f.Status,
                From = f.Legs.OrderBy(l => l.StartTime).Select(l => l.FromAirport.Code).FirstOrDefault(),
                To = f.Legs.OrderByDescending(l => l.StartTime).Select(l => l.ToAirport.Code).FirstOrDefault(),
            }).ToListAsync(ct);
        items.AddRange(flights.Select(f => new UndoneItemResponse { Kind = "flight", Title = $"{f.From} → {f.To}", Status = f.Status }));

        var stays = await _unitOfWork.Accommodations.QueryNoTracking()
            .Where(a => a.EventGuestId == p.Id)
            .Select(a => new { a.Contract.Hotel.Name, a.CheckIn, a.CheckOut }).ToListAsync(ct);
        items.AddRange(stays.Select(s => new UndoneItemResponse
        {
            Kind = "stay",
            Title = s.CheckIn is { } i && s.CheckOut is { } o ? $"{s.Name} · {o.DayNumber - i.DayNumber} nights" : s.Name,
        }));

        var rides = await _unitOfWork.Transports.QueryNoTracking()
            .Where(t => t.EventGuestId == p.Id).Select(t => t.TripStatus).ToListAsync(ct);
        items.AddRange(rides.GroupBy(s => s).Select(g => new UndoneItemResponse
        {
            Kind = "transport", Title = g.Count() == 1 ? "1 ride" : $"{g.Count()} rides", Status = g.Key,
        }));

        var visa = await BuildVisaAsync(p, ct);
        if (visa.Required && visa.Stage != "not_started")
            items.Add(new UndoneItemResponse { Kind = "visa", Title = "Visa application", Status = visa.Stage });

        return items;
    }

    // ── Visa ───────────────────────────────────────────────────────────────

    // Visa is a catalogue service (Services.Code = "visa"), same as readiness reads
    // it: a pending entry = applied, a completed one = issued. VisaStatus "active"
    // without an entry still counts as issued — older rows only ever had the column.
    // Steps and details come from the entry's values read against the service's
    // admin-editable form schema, so nothing here names a field the admin can't change.
    private async Task<VisaTrackerResponse> BuildVisaAsync(EventGuest p, CancellationToken ct)
    {
        var visa = new VisaTrackerResponse
        {
            Required = p.VisaRequired, Status = p.VisaStatus, Country = p.Event?.Destination?.Address,
        };
        if (!p.VisaRequired) { visa.Stage = "not_required"; return visa; }

        var entry = await _unitOfWork.GuestServiceEntries.QueryNoTracking()
            .Where(e => e.EventGuestId == p.Id && e.Service.Code == VisaServiceCode)
            .OrderByDescending(e => e.Status == GuestServiceStatus.Completed).ThenByDescending(e => e.CreatedAt)
            .Select(e => new { e.Status, e.CreatedAt, e.CompletedAt, e.CreatedBy, e.ValuesJson, e.Service.FormSchemaJson })
            .FirstOrDefaultAsync(ct);

        var issued = entry?.Status == GuestServiceStatus.Completed || p.VisaStatus is VisaStatuses.Active or VisaStatuses.ExpiringSoon;
        visa.Stage = issued ? "issued" : entry is not null ? "applied" : "not_started";
        visa.AppliedAt = entry?.CreatedAt;
        visa.IssuedAt = entry?.Status == GuestServiceStatus.Completed ? entry.CompletedAt : null;
        if (entry is null) return visa;

        var values = ParseValues(entry.ValuesJson);
        foreach (var (key, label, type) in SchemaFields(entry.FormSchemaJson))
        {
            values.TryGetValue(key, out var value);
            var isExpiry = key.Contains("expir", StringComparison.OrdinalIgnoreCase);
            if (type == "date" && !isExpiry)
            {
                var date = DateOnly.TryParse(value, out var d) ? d : (DateOnly?)null;
                visa.Steps.Add(new VisaStepResponse { Key = key, Label = label, Date = date, Done = date != null });
            }
            else if (type == "date") visa.ExpiresOn = DateOnly.TryParse(value, out var x) ? x : null;
            else if (type == "file") visa.DocumentUrl ??= string.IsNullOrWhiteSpace(value) ? null : value;
            else if (!string.IsNullOrWhiteSpace(value)) visa.Details.Add(new LabelValueResponse { Key = key, Label = label, Value = value });
        }

        if (entry.CreatedBy is { } by)
        {
            var handler = await _unitOfWork.Users.QueryNoTracking().Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == by, ct);
            visa.HandledBy = handler is null ? null
                : string.Join(" · ", new[] { FullName(handler), handler.Role?.Name }.Where(x => !string.IsNullOrWhiteSpace(x)));
        }
        return visa;
    }

    private static Dictionary<string, string> ParseValues(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateObject().ToDictionary(
                p => p.Name,
                p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString());
        }
        catch (JsonException) { return new(); }
    }

    // {"sections":[{"fields":[{"key","label","type"}]}]} in form order.
    private static IEnumerable<(string Key, string Label, string Type)> SchemaFields(string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson)) yield break;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(schemaJson); } catch (JsonException) { yield break; }
        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("sections", out var sections)) yield break;
            foreach (var section in sections.EnumerateArray())
            {
                if (!section.TryGetProperty("fields", out var fields)) continue;
                foreach (var f in fields.EnumerateArray())
                {
                    var key = f.TryGetProperty("key", out var k) ? k.GetString() : null;
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    yield return (key,
                        f.TryGetProperty("label", out var l) ? l.GetString() : key,
                        f.TryGetProperty("type", out var t) ? t.GetString() : "text");
                }
            }
        }
    }

    // ── Accreditation ──────────────────────────────────────────────────────

    public async Task<ApiResponse<AccreditationResponse>> GetAccreditationAsync(int guestId, Guid eventId, CancellationToken ct)
    {
        var p = await LoadMissionParticipationAsync(guestId, eventId, ct);
        if (p is null) return NotOnMission<AccreditationResponse>();

        var status = await _unitOfWork.Invitations.QueryNoTracking()
            .Where(i => i.EventGuestId == p.Id)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => i.AccreditationStatus)
            .FirstOrDefaultAsync(ct);

        var mission = MapMission(p);
        var visa = await BuildVisaAsync(p, ct);

        string lockReason =
            !p.AccreditationRequired ? "No accreditation is required for this mission"
            : mission.MissionStatus == DelegateMissionStatuses.Cancelled ? "This mission was cancelled"
            : mission.MissionStatus == DelegateMissionStatuses.Completed ? "This mission has ended"
            : mission.MissionStatus == DelegateMissionStatuses.Nominated ? "Unlocks once your nomination is confirmed"
            : visa.Stage is not ("issued" or "not_required") ? "Unlocks when your visa is issued"
            : null;

        List<AccreditationZoneResponse> zones;
        try
        {
            zones = string.IsNullOrWhiteSpace(p.AccreditationZonesJson) ? new()
                : JsonSerializer.Deserialize<List<AccreditationZoneResponse>>(p.AccreditationZonesJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch (JsonException) { zones = new(); }

        var g = p.Guest;
        return ApiResponse<AccreditationResponse>.SuccessResponse(new AccreditationResponse
        {
            Required = p.AccreditationRequired,
            Status = status,
            DelegateName = $"{g.FirstName} {g.LastName}".Trim(),
            PhotoUrl = g.PhotoUrl,
            Organization = p.Organization,
            MissionRole = p.MissionRole?.Name,
            Tier = p.ServiceLevel?.Code,
            ValidFrom = mission.StartDate,
            ValidTo = mission.EndDate,
            QrLocked = lockReason is not null,
            LockReason = lockReason,
            QrPayload = lockReason is null ? p.AccreditationNumber ?? p.PublicId.ToString() : null,
            Number = p.AccreditationNumber,
            Category = p.AccreditationCategory,
            CategoryLabel = p.AccreditationCategoryLabel,
            Zones = zones,
            Issued = lockReason is null && p.AccreditationNumber is not null,
        });
    }

    // ── Stays ──────────────────────────────────────────────────────────────

    public async Task<ApiResponse<StaysResponse>> GetStaysAsync(int guestId, Guid? eventId, CancellationToken ct)
    {
        var eventGuestIds = await ResolveEventGuestIdsAsync(guestId, eventId, ct);
        if (eventGuestIds is null) return ApiResponse<StaysResponse>.NotFoundResponse("Guest not found");

        var stays = await _unitOfWork.Accommodations.QueryNoTracking()
            .Include(a => a.Contract).ThenInclude(c => c.Hotel)
            .Include(a => a.RoomType)
            .Include(a => a.EventGuest).ThenInclude(eg => eg.Event)
            .Where(a => eventGuestIds.Contains(a.EventGuestId))
            .OrderBy(a => a.CheckIn)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var mapped = stays.Select(a => MapStay(a, today)).ToList();

        return ApiResponse<StaysResponse>.SuccessResponse(new StaysResponse
        {
            FundingModel = eventId is null ? null : stays.FirstOrDefault()?.EventGuest?.Event?.FundingModel,
            Upcoming = mapped.Where(s => s.StayState != "completed").ToList(),
            History = mapped.Where(s => s.StayState == "completed").OrderByDescending(s => s.CheckIn).ToList(),
        });
    }

    // today null = use the real today.
    private static AccommodationResponse MapStay(Accommodation a, DateOnly? today)
    {
        var now = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        // Still in the hotel on check-out day, so that day isn't history yet.
        var state = (a.CheckOut ?? a.CheckIn) is { } last && last < now ? "completed"
                  : a.CheckIn is { } first && first <= now ? "checked_in"
                  : "upcoming";
        return new()
        {
            Id = a.PublicId,
            HotelName = a.Contract?.Hotel?.Name,
            HotelImageUrl = a.Contract?.Hotel?.ImageUrl,
            Address = a.Contract?.Hotel?.Address,
            CheckIn = ToDt(a.CheckIn),
            CheckOut = ToDt(a.CheckOut),
            RoomType = a.RoomType?.Name,
            ImageUrl = a.ImageUrl,
            RoomNumber = a.RoomNumber,
            ConfirmationNumber = a.ConfirmationNumber,
            BoardBasis = a.BoardBasis,
            CheckInTime = a.CheckInTime?.ToString("HH:mm"),
            CheckOutTime = a.CheckOutTime?.ToString("HH:mm"),
            Nights = a.CheckIn is { } i && a.CheckOut is { } o ? o.DayNumber - i.DayNumber : null,
            StayState = state,
            EventId = a.EventGuest?.Event?.PublicId,
        };
    }

    /// <summary>Hotels with a room block for this mission — the "change hotel" picker.</summary>
    public async Task<ApiResponse<List<HotelDto>>> GetMissionHotelsAsync(int guestId, Guid eventId, CancellationToken ct)
    {
        var p = await LoadMissionParticipationAsync(guestId, eventId, ct);
        if (p is null) return NotOnMission<List<HotelDto>>();
        return await _inventory.GetContractedHotelsAsync(eventId, ct);
    }

    // ── Places (transport picker) ──────────────────────────────────────────

    public async Task<ApiResponse<PlacesResponse>> GetPlacesAsync(int guestId, Guid eventId, CancellationToken ct)
    {
        var p = await LoadMissionParticipationAsync(guestId, eventId, ct);
        if (p is null) return NotOnMission<PlacesResponse>();

        var suggested = new List<PlaceResponse>();

        // Their hotels on this mission.
        suggested.AddRange(await _unitOfWork.Accommodations.QueryNoTracking()
            .Where(a => a.EventGuestId == p.Id && a.Contract.Hotel.Location != null)
            .Select(a => new PlaceResponse
            {
                Id = a.Contract.Hotel.Location.PublicId, Name = a.Contract.Hotel.Name,
                Address = a.Contract.Hotel.Location.Address, Type = "hotel",
                Latitude = a.Contract.Hotel.Location.Latitude, Longitude = a.Contract.Hotel.Location.Longitude,
            }).ToListAsync(ct));

        // The mission venue and every session venue.
        var venueIds = await _unitOfWork.Sessions.QueryNoTracking()
            .Where(s => s.EventId == p.EventId && s.VenueId != null).Select(s => s.VenueId.Value).ToListAsync(ct);
        if (p.Event.VenueId is { } v) venueIds.Add(v);
        suggested.AddRange(await _unitOfWork.Venues.QueryNoTracking()
            .Where(x => venueIds.Contains(x.Id) && x.Location != null)
            .Select(x => new PlaceResponse
            {
                Id = x.Location.PublicId, Name = x.Name, Address = x.Location.Address, Type = "venue",
                Latitude = x.Location.Latitude, Longitude = x.Location.Longitude,
            }).ToListAsync(ct));

        // Airports on their flights.
        var legEnds = await _unitOfWork.FlightLegs.QueryNoTracking()
            .Where(l => l.Flight.EventGuestId == p.Id)
            .Select(l => new { l.FromAirportId, l.ToAirportId }).ToListAsync(ct);
        var airportIds = legEnds.SelectMany(l => new[] { l.FromAirportId, l.ToAirportId })
            .Where(id => id != null).Select(id => id.Value).Distinct().ToList();
        suggested.AddRange(await _unitOfWork.AirportData.QueryNoTracking()
            .Where(a => airportIds.Contains(a.Id) && a.Location != null)
            .Select(a => new PlaceResponse
            {
                Id = a.Location.PublicId, Name = $"{a.Code} · {a.City}", Address = a.Location.Address, Type = "airport",
                Latitude = a.Location.Latitude, Longitude = a.Location.Longitude,
            }).ToListAsync(ct));

        suggested = suggested.GroupBy(x => x.Id).Select(g => g.First()).ToList();
        var seen = suggested.Select(x => x.Id).ToHashSet();

        var rides = await _unitOfWork.Transports.QueryNoTracking()
            .Include(t => t.PickupLocation).Include(t => t.DropoffLocation)
            .Where(t => t.EventGuest.GuestId == guestId)
            .OrderByDescending(t => t.CreatedAt).Take(50)
            .ToListAsync(ct);
        var recent = rides.SelectMany(t => new[] { t.PickupLocation, t.DropoffLocation })
            .Where(l => l != null)
            .Select(l => new PlaceResponse
            {
                Id = l.PublicId, Name = l.Address, Address = l.Address, Type = l.Type,
                Latitude = l.Latitude, Longitude = l.Longitude,
            }).ToList();

        return ApiResponse<PlacesResponse>.SuccessResponse(new PlacesResponse
        {
            Suggested = suggested,
            Recent = recent.Where(x => !seen.Contains(x.Id)).GroupBy(x => x.Id).Select(g => g.First()).ToList(),
        });
    }

    // A known location id, or a typed address / current position saved as a new
    // "custom" Locations row. Null = neither given, or the id doesn't exist.
    private async Task<int?> ResolvePlaceAsync(Guid locationId, PlaceInput place, CancellationToken ct)
    {
        if (locationId != Guid.Empty) return await ResolveLocationIdAsync(locationId, ct);

        var address = place?.Address?.Trim();
        var hasCoords = !string.IsNullOrWhiteSpace(place?.Latitude) && !string.IsNullOrWhiteSpace(place?.Longitude);
        if (string.IsNullOrWhiteSpace(address) && !hasCoords) return null;
        if (hasCoords && (!double.TryParse(place.Latitude, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat) || lat is < -90 or > 90
                       || !double.TryParse(place.Longitude, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lng) || lng is < -180 or > 180))
            return null;

        var location = new Location
        {
            Address = address?.Length > 300 ? address[..300] : address,
            Latitude = hasCoords ? place.Latitude.Trim() : null,
            Longitude = hasCoords ? place.Longitude.Trim() : null,
            Type = "custom",
        };
        await _unitOfWork.Locations.AddAsync(location, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return location.Id;
    }

    // ── Transport request status (the "finding a chauffeur" screen polls this) ──

    public async Task<ApiResponse<TransportationResponse>> GetTransportRequestAsync(int guestId, Guid transportId, CancellationToken ct)
    {
        var eventGuestIds = await ResolveEventGuestIdsAsync(guestId, null, ct);
        if (eventGuestIds is null) return ApiResponse<TransportationResponse>.NotFoundResponse("Guest not found");

        var t = await _unitOfWork.Transports.QueryNoTracking()
            .Include(x => x.PickupLocation).Include(x => x.DropoffLocation)
            .Include(x => x.Vehicle).ThenInclude(v => v.VehicleType)
            .Include(x => x.Driver).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(x => x.PublicId == transportId && eventGuestIds.Contains(x.EventGuestId), ct);

        return t is null
            ? ApiResponse<TransportationResponse>.NotFoundResponse("Transport request not found")
            : ApiResponse<TransportationResponse>.SuccessResponse(MapTransport(t));
    }

    // ── Booking change requests ────────────────────────────────────────────

    public async Task<ApiResponse<ChangeRequestResponse>> CreateChangeRequestAsync(int guestId, ChangeRequestCreate request, CancellationToken ct)
    {
        if (request is null || request.EventId == Guid.Empty || request.BookingId == Guid.Empty)
            return ApiResponse<ChangeRequestResponse>.ErrorResponse("eventId and bookingId are required");

        var kind = request.BookingKind?.Trim().ToLowerInvariant();
        var type = request.ChangeType?.Trim().ToLowerInvariant();
        if (!BookingChangeTypes.IsValid(kind, type))
            return ApiResponse<ChangeRequestResponse>.ErrorResponse(
                $"For a flight: {string.Join(", ", BookingChangeTypes.Flight)}. For a stay: {string.Join(", ", BookingChangeTypes.Stay)}.");

        var notes = request.Notes?.Trim();
        if (notes?.Length > 2000) return ApiResponse<ChangeRequestResponse>.ErrorResponse("Notes cannot exceed 2000 characters");

        var p = await LoadMissionParticipationAsync(guestId, request.EventId, ct);
        if (p is null) return NotOnMission<ChangeRequestResponse>();
        if (MapMission(p).MissionStatus is DelegateMissionStatuses.Completed or DelegateMissionStatuses.Cancelled)
            return ApiResponse<ChangeRequestResponse>.ConflictResponse("This mission is closed to changes");

        var row = new BookingChangeRequest
        {
            EventGuestId = p.Id, BookingKind = kind, ChangeType = type,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
            Status = BookingChangeStatuses.Requested,
        };

        if (kind == BookingChangeKinds.Flight)
        {
            row.FlightId = await _unitOfWork.Flights.QueryNoTracking()
                .Where(f => f.PublicId == request.BookingId && f.EventGuestId == p.Id)
                .Select(f => (int?)f.Id).FirstOrDefaultAsync(ct);
            if (row.FlightId is null) return ApiResponse<ChangeRequestResponse>.NotFoundResponse("Flight not found");

            if (type == BookingChangeTypes.RouteChange)
            {
                row.FromAirportId = await AirportIdAsync(request.FromAirportId, ct);
                row.ToAirportId = await AirportIdAsync(request.ToAirportId, ct);
                if (row.FromAirportId is null || row.ToAirportId is null || row.FromAirportId == row.ToAirportId)
                    return ApiResponse<ChangeRequestResponse>.ErrorResponse("A route change needs two different airports");
            }
        }
        else
        {
            row.AccommodationId = await _unitOfWork.Accommodations.QueryNoTracking()
                .Where(a => a.PublicId == request.BookingId && a.EventGuestId == p.Id)
                .Select(a => (int?)a.Id).FirstOrDefaultAsync(ct);
            if (row.AccommodationId is null) return ApiResponse<ChangeRequestResponse>.NotFoundResponse("Stay not found");

            if (type == BookingChangeTypes.ChangeHotel)
            {
                // Only hotels this mission holds a room block at.
                row.HotelId = request.HotelId is { } hid ? await _unitOfWork.EventHotelContracts.QueryNoTracking()
                    .Where(c => c.EventId == p.EventId && c.Hotel.PublicId == hid)
                    .Select(c => (int?)c.AccommodationHotelId).FirstOrDefaultAsync(ct) : null;
                if (row.HotelId is null)
                    return ApiResponse<ChangeRequestResponse>.ErrorResponse("Pick one of the hotels contracted for this mission");
            }
        }

        row.SetCreationAudit(p.Guest.UserId);
        await _unitOfWork.BookingChangeRequests.AddAsync(row, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await NotifyCoordinatorAsync(p, $"Change request · {type.Replace('_', ' ')}",
            $"{p.Guest.FirstName} {p.Guest.LastName} ({p.Event.Title}) asked for a {kind} change{(row.Notes is null ? "" : ": " + row.Notes)}", "change-request", ct);

        var saved = (await LoadChangeRequestsAsync(q => q.Where(r => r.Id == row.Id), ct)).Single();
        return ApiResponse<ChangeRequestResponse>.SuccessResponse(saved, "Sent to your mission coordinator");
    }

    private Task<int?> AirportIdAsync(Guid? id, CancellationToken ct)
        => id is { } g && g != Guid.Empty
            ? _unitOfWork.AirportData.QueryNoTracking().Where(a => a.PublicId == g).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct)
            : Task.FromResult<int?>(null);

    public async Task<ApiResponse<List<ChangeRequestResponse>>> GetChangeRequestsAsync(int guestId, Guid? eventId, CancellationToken ct)
    {
        var rows = await LoadChangeRequestsAsync(q => q
            .Where(r => r.EventGuest.GuestId == guestId)
            .Where(r => eventId == null || r.EventGuest.Event.PublicId == eventId), ct);
        return ApiResponse<List<ChangeRequestResponse>>.SuccessResponse(rows);
    }

    private async Task<List<ChangeRequestResponse>> LoadChangeRequestsAsync(
        Func<IQueryable<BookingChangeRequest>, IQueryable<BookingChangeRequest>> filter, CancellationToken ct)
    {
        var rows = await filter(_unitOfWork.BookingChangeRequests.QueryNoTracking()
                .Include(r => r.EventGuest).ThenInclude(eg => eg.Event)
                .Include(r => r.Flight).ThenInclude(f => f.Legs).ThenInclude(l => l.FromAirport)
                .Include(r => r.Flight).ThenInclude(f => f.Legs).ThenInclude(l => l.ToAirport)
                .Include(r => r.Accommodation).ThenInclude(a => a.Contract).ThenInclude(c => c.Hotel)
                .Include(r => r.FromAirport).Include(r => r.ToAirport).Include(r => r.Hotel))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(MapChangeRequest).ToList();
    }

    internal static ChangeRequestResponse MapChangeRequest(BookingChangeRequest r)
    {
        string label = null;
        if (r.Flight is { } f)
        {
            var legs = f.Legs.OrderBy(l => l.StartTime).ToList();
            label = $"{legs.FirstOrDefault()?.FromAirport?.Code} → {legs.LastOrDefault()?.ToAirport?.Code} · {legs.FirstOrDefault()?.FlightNumber}".Trim(' ', '·');
        }
        else if (r.Accommodation is { } a)
            label = $"{a.Contract?.Hotel?.Name} · {a.CheckIn:dd MMM} – {a.CheckOut:dd MMM}";

        return new ChangeRequestResponse
        {
            Id = r.PublicId,
            EventId = r.EventGuest?.Event?.PublicId ?? Guid.Empty,
            BookingKind = r.BookingKind,
            BookingId = r.Flight?.PublicId ?? r.Accommodation?.PublicId,
            BookingLabel = label,
            ChangeType = r.ChangeType,
            Notes = r.Notes,
            FromAirport = r.FromAirport?.Code,
            ToAirport = r.ToAirport?.Code,
            Hotel = r.Hotel?.Name,
            Status = r.Status,
            ResolutionNote = r.ResolutionNote,
            CreatedAt = r.CreatedAt,
            ResolvedAt = r.ResolvedAt,
        };
    }

    // ── Help requests ──────────────────────────────────────────────────────

    private const int MaxHelpDescriptionLength = 2000;

    public async Task<ApiResponse<HelpRequestResponse>> RaiseHelpRequestAsync(int guestId, HelpRequest request, CancellationToken ct)
    {
        if (request is null || request.EventId == Guid.Empty)
            return ApiResponse<HelpRequestResponse>.ErrorResponse("eventId is required");

        var category = request.Category?.Trim().ToLowerInvariant();
        if (!IncidentCategories.IsValid(category))
            return ApiResponse<HelpRequestResponse>.ErrorResponse(
                $"Category must be one of: {string.Join(", ", IncidentCategories.All)}");

        var description = request.Description?.Trim();
        if (string.IsNullOrWhiteSpace(description))
            return ApiResponse<HelpRequestResponse>.ErrorResponse("Tell your coordinator what's happening");
        if (description.Length > MaxHelpDescriptionLength)
            return ApiResponse<HelpRequestResponse>.ErrorResponse($"Description cannot exceed {MaxHelpDescriptionLength} characters");

        var p = await LoadMissionParticipationAsync(guestId, request.EventId, ct);
        if (p is null) return NotOnMission<HelpRequestResponse>();

        var userId = p.Guest.UserId;
        var incident = new Incident
        {
            EventId = p.EventId,
            EventGuestId = p.Id,
            Category = category,
            // ponytail: the app sends no severity — medical is high, the rest
            // medium; the coordinator re-grades it on the incidents board.
            Severity = category == IncidentCategories.Medical ? IncidentSeverities.High : IncidentSeverities.Medium,
            Status = IncidentStatuses.Open,
            Description = description,
            RaisedBy = userId,
            RaisedVia = IncidentChannels.App,
        };
        incident.SetCreationAudit(userId);

        await _unitOfWork.Incidents.AddAsync(incident, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await NotifyCoordinatorAsync(p, $"Help request · {category}",
            $"{p.Guest.FirstName} {p.Guest.LastName} ({p.Event.Title}): {description}", "incident", ct);
        return ApiResponse<HelpRequestResponse>.SuccessResponse(MapHelp(incident, request.EventId), "Sent to your mission coordinator");
    }

    // Best-effort: the row is already saved and on the portal's board; a failed
    // push must not turn it into an error for the delegate.
    private async Task NotifyCoordinatorAsync(EventGuest p, string title, string message, string type, CancellationToken ct)
    {
        var coordinator = CoordinatorOf(p);
        if (coordinator is null) return;
        try
        {
            await _notifications.SendToUserAsync(coordinator.Id,
                new NotificationContent { Title = title, Message = message, Type = type }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notifying coordinator ({Type}) failed for participation {EventGuestId}", type, p.Id);
        }
    }

    public async Task<ApiResponse<List<HelpRequestResponse>>> GetHelpRequestsAsync(int guestId, Guid? eventId, CancellationToken ct)
    {
        var guest = await GetGuestAsync(guestId, ct);
        if (guest is null) return ApiResponse<List<HelpRequestResponse>>.NotFoundResponse("Guest not found");

        // Only what they raised themselves — incidents an officer logged about
        // them are the coordinator's notes, not the delegate's requests.
        var rows = await _unitOfWork.Incidents.QueryNoTracking()
            .Where(i => i.EventGuest.GuestId == guest.Id && i.RaisedBy == guest.UserId)
            .Where(i => eventId == null || i.Event.PublicId == eventId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new { Incident = i, EventPublicId = i.Event.PublicId })
            .ToListAsync(ct);

        return ApiResponse<List<HelpRequestResponse>>.SuccessResponse(
            rows.Select(r => MapHelp(r.Incident, r.EventPublicId)).ToList());
    }

    private static HelpRequestResponse MapHelp(Incident i, Guid eventId) => new()
    {
        Id = i.PublicId, EventId = eventId, Category = i.Category, Severity = i.Severity,
        Status = i.Status, Description = i.Description, CreatedAt = i.CreatedAt, ResolvedAt = i.ResolvedAt,
    };

    // ── Programme (itinerary) ──────────────────────────────────────────────

    // The mission's whole programme, with how many journal entries the caller
    // filed against each item. TBC = no time set yet.
    private async Task<List<ProgrammeItemResponse>> BuildProgrammeAsync(
        List<int> eventGuestIds, Guid eventId, DateOnly? date, CancellationToken ct)
    {
        var ev = await _unitOfWork.Events.QueryNoTracking()
            .Where(e => e.PublicId == eventId).Select(e => new { e.Id, e.StartDate }).FirstOrDefaultAsync(ct);
        if (ev is null) return new();

        var counts = await _unitOfWork.MissionJournalEntries.QueryNoTracking()
            .Where(j => eventGuestIds.Contains(j.EventGuestId) && j.SessionId != null)
            .GroupBy(j => j.SessionId.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var sessions = await _unitOfWork.Sessions.QueryNoTracking()
            .Where(s => s.EventId == ev.Id)
            .Where(s => date == null || s.Date == date)
            .OrderBy(s => s.Date).ThenBy(s => s.Time)
            .ToListAsync(ct);

        return sessions.Select(s => new ProgrammeItemResponse
        {
            Id = s.PublicId, Title = s.Title, Date = s.Date,
            Time = string.IsNullOrWhiteSpace(s.Time) ? null : s.Time,
            VenueName = s.VenueName, Room = s.Room,
            DayNumber = s.Date is { } d && ev.StartDate is { } st ? d.DayNumber - st.DayNumber + 1 : null,
            JournalCount = counts.TryGetValue(s.Id, out var n) ? n : 0,
        }).ToList();
    }

    // ── Mission journal ────────────────────────────────────────────────────

    // Writable only while the delegate is actually on the mission (upcoming or
    // live): "attachments lock when the mission closes", and a nomination that
    // isn't confirmed yet has no journal.
    private static string JournalLockReason(EventGuest p) => MapMission(p).MissionStatus switch
    {
        DelegateMissionStatuses.Nominated => "Your journal opens once you're confirmed on the delegation",
        DelegateMissionStatuses.Completed => "The journal locked when this mission closed",
        DelegateMissionStatuses.Cancelled => "This mission was cancelled",
        _ => null,
    };

    private async Task<JournalSummaryResponse> JournalSummaryAsync(EventGuest p, CancellationToken ct)
    {
        var stats = await _unitOfWork.MissionJournalEntries.QueryNoTracking()
            .Where(j => j.EventGuestId == p.Id)
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), InReport = g.Count(j => j.IncludedInReport), Last = g.Max(j => j.CreatedAt) })
            .FirstOrDefaultAsync(ct);

        return new JournalSummaryResponse
        {
            Total = stats?.Total ?? 0,
            InReport = stats?.InReport ?? 0,
            LastAddedAt = stats?.Last,
            Editable = JournalLockReason(p) is null,
        };
    }

    public async Task<ApiResponse<JournalResponse>> GetJournalAsync(int guestId, Guid eventId, CancellationToken ct)
    {
        var p = await LoadMissionParticipationAsync(guestId, eventId, ct);
        if (p is null) return NotOnMission<JournalResponse>();

        var entries = await _unitOfWork.MissionJournalEntries.QueryNoTracking()
            .Include(j => j.Session)
            .Where(j => j.EventGuestId == p.Id)
            .OrderBy(j => j.CreatedAt)
            .ToListAsync(ct);

        // The mission's programme, so days with an event but nothing captured
        // yet still show ("nothing yet" + the add button).
        var programme = await _unitOfWork.Sessions.QueryNoTracking()
            .Where(s => s.EventId == p.EventId && s.Date != null)
            .OrderBy(s => s.Time)
            .Select(s => new { Date = s.Date.Value, s.Title })
            .ToListAsync(ct);

        var start = p.Event.StartDate;
        var days = programme.Select(x => x.Date).Concat(entries.Select(e => e.EntryDate))
            .Distinct().Order()
            .Select(d => new JournalDayResponse
            {
                Date = d,
                DayNumber = start is { } s ? d.DayNumber - s.DayNumber + 1 : null,
                Programme = programme.Where(x => x.Date == d).Select(x => x.Title).ToList(),
                Entries = entries.Where(e => e.EntryDate == d).Select(MapJournal).ToList(),
            }).ToList();

        return ApiResponse<JournalResponse>.SuccessResponse(new JournalResponse
        {
            Summary = await JournalSummaryAsync(p, ct),
            Days = days,
        });
    }

    public async Task<ApiResponse<JournalEntryResponse>> AddJournalEntryAsync(
        int guestId, Guid eventId, CreateJournalEntryRequest request, CancellationToken ct)
    {
        if (request is null) return ApiResponse<JournalEntryResponse>.ErrorResponse("Request body is required");

        var type = request.Type?.Trim().ToLowerInvariant();
        if (!JournalEntryTypes.IsValid(type))
            return ApiResponse<JournalEntryResponse>.ErrorResponse(
                $"Type must be one of: {string.Join(", ", JournalEntryTypes.All)}");

        var visibility = string.IsNullOrWhiteSpace(request.Visibility) ? JournalVisibilities.Shared : request.Visibility.Trim().ToLowerInvariant();
        if (!JournalVisibilities.IsValid(visibility))
            return ApiResponse<JournalEntryResponse>.ErrorResponse("Visibility must be private or shared");

        var p = await LoadMissionParticipationAsync(guestId, eventId, ct);
        if (p is null) return NotOnMission<JournalEntryResponse>();
        if (JournalLockReason(p) is { } locked) return ApiResponse<JournalEntryResponse>.ConflictResponse(locked);

        var e = p.Event;
        if ((e.StartDate is { } s && request.Date < s) || (e.EndDate is { } en && request.Date > en))
            return ApiResponse<JournalEntryResponse>.ErrorResponse("Date must fall within the mission");

        int? sessionId = null;
        if (request.SessionId is { } sid && sid != Guid.Empty)
        {
            sessionId = await _unitOfWork.Sessions.QueryNoTracking()
                .Where(x => x.PublicId == sid && x.EventId == p.EventId)
                .Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (sessionId is null) return ApiResponse<JournalEntryResponse>.ErrorResponse("Programme item not found on this mission");
        }

        var title = request.Title?.Trim();
        var body = request.Body?.Trim();
        string fileUrl = null;
        long? size = null;

        if (type == JournalEntryTypes.Note)
        {
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
                return ApiResponse<JournalEntryResponse>.ErrorResponse("A note needs a title or some text");
        }
        else
        {
            var file = request.File;
            if (file is null || file.Length == 0)
                return ApiResponse<JournalEntryResponse>.ErrorResponse("A file is required for this type");

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (!JournalEntryTypes.Extensions(type).Contains(ext))
                return ApiResponse<JournalEntryResponse>.ErrorResponse(
                    $"Allowed for {type}: {string.Join(", ", JournalEntryTypes.Extensions(type))}");

            var max = JournalEntryTypes.MaxBytes(type);
            if (file.Length > max)
                return ApiResponse<JournalEntryResponse>.ErrorResponse($"{type} files can be up to {max / (1024 * 1024)} MB");

            // ponytail: video's 3-minute cap isn't checked — the app trims on
            // capture; server-side would need a media probe.
            await using var stream = file.OpenReadStream();
            fileUrl = await _blob.UploadStreamAsync(stream, file.FileName, ct: ct);
            size = file.Length;
            if (string.IsNullOrWhiteSpace(title)) title = Path.GetFileName(file.FileName);
        }

        var userId = p.Guest.UserId;
        var entry = new MissionJournalEntry
        {
            EventGuestId = p.Id,
            EntryDate = request.Date,
            SessionId = sessionId,
            Type = type,
            Title = title?.Length > 300 ? title[..300] : title,
            Body = body?.Length > 4000 ? body[..4000] : body,
            FileUrl = fileUrl,
            FileSizeBytes = size,
            IncludedInReport = request.IncludedInReport,
            Visibility = visibility,
        };
        entry.SetCreationAudit(userId);

        await _unitOfWork.MissionJournalEntries.AddAsync(entry, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<JournalEntryResponse>.SuccessResponse(MapJournal(entry), "Added to your journal");
    }

    // The caller's own entry, with its participation loaded for the lock check.
    // Null for someone else's — indistinguishable from not existing.
    private Task<MissionJournalEntry> FindOwnEntryAsync(int guestId, Guid entryId, CancellationToken ct)
        => _unitOfWork.MissionJournalEntries.Query()
            .Include(j => j.Session)
            .Include(j => j.EventGuest).ThenInclude(eg => eg.Event)
            .Include(j => j.EventGuest).ThenInclude(eg => eg.Guest)
            .FirstOrDefaultAsync(j => j.PublicId == entryId && j.EventGuest.GuestId == guestId, ct);

    public async Task<ApiResponse<JournalEntryResponse>> UpdateJournalEntryAsync(
        int guestId, Guid entryId, UpdateJournalEntryRequest request, CancellationToken ct)
    {
        var entry = await FindOwnEntryAsync(guestId, entryId, ct);
        if (entry is null) return ApiResponse<JournalEntryResponse>.NotFoundResponse("Journal entry not found");
        if (JournalLockReason(entry.EventGuest) is { } locked) return ApiResponse<JournalEntryResponse>.ConflictResponse(locked);

        if (request?.Visibility is { } v)
        {
            v = v.Trim().ToLowerInvariant();
            if (!JournalVisibilities.IsValid(v)) return ApiResponse<JournalEntryResponse>.ErrorResponse("Visibility must be private or shared");
            entry.Visibility = v;
        }
        if (request?.IncludedInReport is { } inReport) entry.IncludedInReport = inReport;

        entry.SetUpdateAudit(entry.EventGuest.Guest.UserId);
        _unitOfWork.MissionJournalEntries.Update(entry);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<JournalEntryResponse>.SuccessResponse(MapJournal(entry));
    }

    public async Task<ApiResponse<bool>> DeleteJournalEntryAsync(int guestId, Guid entryId, CancellationToken ct)
    {
        var entry = await FindOwnEntryAsync(guestId, entryId, ct);
        if (entry is null) return ApiResponse<bool>.NotFoundResponse("Journal entry not found");
        if (JournalLockReason(entry.EventGuest) is { } locked) return ApiResponse<bool>.ConflictResponse(locked);

        // Soft delete; the blob stays, like every other soft-deleted upload here.
        entry.MarkAsDeleted(entry.EventGuest.Guest.UserId);
        _unitOfWork.MissionJournalEntries.Update(entry);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<bool>.SuccessResponse(true, "Removed from your journal");
    }

    private static JournalEntryResponse MapJournal(MissionJournalEntry j) => new()
    {
        Id = j.PublicId, Date = j.EntryDate, SessionId = j.Session?.PublicId,
        Type = j.Type, Title = j.Title, Body = j.Body,
        FileUrl = j.FileUrl, FileSizeBytes = j.FileSizeBytes,
        IncludedInReport = j.IncludedInReport, Visibility = j.Visibility, CreatedAt = j.CreatedAt,
    };

    // ── Post-mission report ────────────────────────────────────────────────

    // "Your report opens after the mission"; approved = locked by the portal.
    private static string ReportLockReason(EventGuest p, PostMissionReport report) =>
        report?.Status == PostMissionReportStatuses.Approved ? "This report has been approved"
        : MapMission(p).MissionStatus switch
        {
            DelegateMissionStatuses.Completed => null,
            DelegateMissionStatuses.Cancelled => "Cancelled missions don't need a post-mission report",
            _ => "Your report opens after the mission",
        };

    public async Task<ApiResponse<DelegateReportResponse>> GetReportAsync(int guestId, Guid eventId, CancellationToken ct)
    {
        var p = await LoadMissionParticipationAsync(guestId, eventId, ct);
        if (p is null) return NotOnMission<DelegateReportResponse>();

        var report = await _unitOfWork.PostMissionReports.QueryNoTracking()
            .FirstOrDefaultAsync(r => r.EventGuestId == p.Id, ct);

        var journal = await _unitOfWork.MissionJournalEntries.QueryNoTracking()
            .Include(j => j.Session)
            .Where(j => j.EventGuestId == p.Id)
            .OrderBy(j => j.EntryDate).ThenBy(j => j.CreatedAt)
            .ToListAsync(ct);

        var hotels = await _unitOfWork.Accommodations.QueryNoTracking()
            .Where(a => a.EventGuestId == p.Id).Select(a => a.Contract.Hotel.Name).Distinct().ToListAsync(ct);
        var vehicles = await _unitOfWork.Transports.QueryNoTracking()
            .Where(t => t.EventGuestId == p.Id && t.Vehicle != null).Select(t => t.Vehicle.VehicleNumber).Distinct().ToListAsync(ct);

        var locked = ReportLockReason(p, report);
        var approved = report?.Status == PostMissionReportStatuses.Approved;

        return ApiResponse<DelegateReportResponse>.SuccessResponse(new DelegateReportResponse
        {
            Status = report?.Status ?? PostMissionReportStatuses.NotSubmitted,
            Narrative = report?.Narrative,
            KeyOutcomes = report?.KeyOutcomes,
            SubmittedOn = report?.SubmittedOn,
            ApprovedOn = approved ? report.UpdatedAt : null,
            ApprovedBy = approved ? await UserNameAsync(report.UpdatedBy, ct) : null,
            Editable = locked is null,
            LockedReason = locked,
            Evidence = journal.Where(j => j.IncludedInReport).Select(MapJournal).ToList(),
            AutoFill = new ReportAutoFillResponse
            {
                From = ToDt(p.Event.StartDate),
                To = ToDt(p.Event.EndDate),
                ProgrammeAttended = journal.Where(j => j.Session != null).Select(j => j.Session.Title).Distinct().ToList(),
                Hotels = hotels,
                Vehicles = vehicles,
                JournalTotal = journal.Count,
                InReport = journal.Count(j => j.IncludedInReport),
            },
        });
    }

    // Writes through MissionReportService so the rules (no empty submit, locked
    // once approved) live in one place for the portal and the app.
    public async Task<ApiResponse<DelegateReportResponse>> SaveReportAsync(
        int guestId, Guid eventId, SaveDelegateReportRequest request, CancellationToken ct)
    {
        var p = await LoadMissionParticipationAsync(guestId, eventId, ct);
        if (p is null) return NotOnMission<DelegateReportResponse>();

        var existing = await _unitOfWork.PostMissionReports.QueryNoTracking()
            .FirstOrDefaultAsync(r => r.EventGuestId == p.Id, ct);
        if (ReportLockReason(p, existing) is { } locked) return ApiResponse<DelegateReportResponse>.ConflictResponse(locked);

        var outcomes = request?.KeyOutcomes?.Trim();
        if (outcomes?.Length > 2000) return ApiResponse<DelegateReportResponse>.ErrorResponse("Key outcomes cannot exceed 2000 characters");

        var saved = await _reports.SaveReportAsync(new SaveReportRequest
        {
            Id = p.PublicId,
            Narrative = request?.Narrative,
            Submit = request?.Submit ?? false,
        }, p.Guest.UserId, ct);
        if (!saved.Success) return Fail<DelegateReportResponse, PostMissionReportResponse>(saved);

        // Key outcomes is an app-only field the portal service doesn't know about.
        if (request?.KeyOutcomes != null)
        {
            var row = await _unitOfWork.PostMissionReports.Query().FirstAsync(r => r.EventGuestId == p.Id, ct);
            row.KeyOutcomes = string.IsNullOrWhiteSpace(outcomes) ? null : outcomes;
            _unitOfWork.PostMissionReports.Update(row);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        var result = await GetReportAsync(guestId, eventId, ct);
        result.Message = request?.Submit == true ? "Report submitted" : "Draft saved";
        return result;
    }
}
