using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using ClosedXML.Excel;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Event;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

public class EventService(
    IUnitOfWork _unitOfWork,
    IMapper _mapper,
    ILogger<EventService> _logger,
    IBlobService _blobService,
    IBackgroundJobClient _backgroundJobClient,
    IImportBatchService _importBatchService) : IEventService
{
    private static readonly string[] ValidStatuses = { "planning", "active", "completed", "cancelled" };

    // Allowed lifecycle transitions (see R&D §4.2).
    private static readonly Dictionary<string, string[]> Transitions = new()
    {
        ["planning"]  = new[] { "active", "cancelled" },
        ["active"]    = new[] { "completed", "cancelled" },
        ["completed"] = Array.Empty<string>(),
        ["cancelled"] = new[] { "planning" },
    };

    public async Task<ApiResponse<PaginatedResponse<EventResponse>>> GetEventsAsync(PagedRequest request, string status, CancellationToken ct = default)
    {
        IQueryable<Event> query = _unitOfWork.Events.QueryNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(e => e.Title.Contains(term) || e.VenueName.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.StartDate)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            // Venue is needed for SessionResponse.VenueId — without it the
            // session edit form opens with an empty venue dropdown. Also needed
            // directly on Event for EventResponse.VenueId.
            .Include(e => e.Venue)
            .Include(e => e.Sessions).ThenInclude(s => s.Venue)
            .ToListAsync(ct);

        var mapped = _mapper.Map<List<EventResponse>>(items);
        var paged = new PaginatedResponse<EventResponse>(mapped, total, request.PageNumber, request.PageSize);
        return ApiResponse<PaginatedResponse<EventResponse>>.SuccessResponse(paged);
    }

    public async Task<ApiResponse<EventResponse>> GetEventByIdAsync(Guid id, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.Query()
            .Include(e => e.Venue)
            .Include(e => e.Sessions).ThenInclude(s => s.Venue)
            .FirstOrDefaultAsync(e => e.PublicId == id, ct);

        if (ev == null)
            return ApiResponse<EventResponse>.NotFoundResponse("Event not found");

        return ApiResponse<EventResponse>.SuccessResponse(_mapper.Map<EventResponse>(ev));
    }

    public async Task<ApiResponse<EventResponse>> CreateEventAsync(CreateEventRequest request, int userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return ApiResponse<EventResponse>.ErrorResponse("Title is required");

        var status = string.IsNullOrWhiteSpace(request.Status) ? "planning" : request.Status.ToLowerInvariant();
        if (!ValidStatuses.Contains(status))
            return ApiResponse<EventResponse>.ErrorResponse($"Invalid status '{request.Status}'");

        var ev = _mapper.Map<Event>(request);
        ev.Status = status;
        ev.AppKey = await UniqueAppKeyAsync(Slugify(request.Title), ct);

        if (request.VenueId.HasValue && request.VenueId.Value != Guid.Empty)
        {
            var venue = await _unitOfWork.Venues.GetByPublicIdAsync(request.VenueId.Value, ct);
            if (venue == null)
                return ApiResponse<EventResponse>.NotFoundResponse("Venue not found");
            ev.VenueId = venue.Id;
            ev.VenueName = venue.Name;
            // Set the nav too: the response is mapped off this instance, and the
            // mapper reads Venue.PublicId.
            ev.Venue = venue;
        }

        ev.SetCreationAudit(userId);

        await _unitOfWork.Events.AddAsync(ev, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<EventResponse>.SuccessResponse(_mapper.Map<EventResponse>(ev), "Event created");
    }

    public async Task<ApiResponse<EventResponse>> UpdateEventAsync(Guid id, UpdateEventRequest request, int userId, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByPublicIdAsync(id, ct);
        if (ev == null)
            return ApiResponse<EventResponse>.NotFoundResponse("Event not found");

        ev.Title = request.Title ?? ev.Title;
        ev.Type = request.Type ?? ev.Type;
        ev.Theme = request.Theme ?? ev.Theme;

        if (request.VenueId.HasValue && request.VenueId.Value != Guid.Empty)
        {
            var venue = await _unitOfWork.Venues.GetByPublicIdAsync(request.VenueId.Value, ct);
            if (venue == null)
                return ApiResponse<EventResponse>.NotFoundResponse("Venue not found");
            ev.VenueId = venue.Id;
            ev.VenueName = venue.Name;
            ev.Venue = venue;
        }
        else
        {
            ev.VenueName = request.VenueName ?? ev.VenueName;
        }

        ev.StartDate = request.StartDate ?? ev.StartDate;
        ev.EndDate = request.EndDate ?? ev.EndDate;
        ev.ImageUrl = request.ImageUrl ?? ev.ImageUrl;
        ev.ThemeAccent = request.ThemeAccent ?? ev.ThemeAccent;
        ev.ThemeSecondary = request.ThemeSecondary ?? ev.ThemeSecondary;
        ev.LogoDarkUrl = request.LogoDarkUrl ?? ev.LogoDarkUrl;
        ev.LogoLightUrl = request.LogoLightUrl ?? ev.LogoLightUrl;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.ToLowerInvariant();
            if (!ValidStatuses.Contains(status))
                return ApiResponse<EventResponse>.ErrorResponse($"Invalid status '{request.Status}'");
            ev.Status = status;
        }

        ev.SetUpdateAudit(userId);
        _unitOfWork.Events.Update(ev);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<EventResponse>.SuccessResponse(_mapper.Map<EventResponse>(ev), "Event updated");
    }

    public async Task<ApiResponse<EventResponse>> UpdateStatusAsync(Guid id, string status, int userId, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByPublicIdAsync(id, ct);
        if (ev == null)
            return ApiResponse<EventResponse>.NotFoundResponse("Event not found");

        var target = (status ?? string.Empty).ToLowerInvariant();
        if (!ValidStatuses.Contains(target))
            return ApiResponse<EventResponse>.ErrorResponse($"Invalid status '{status}'");

        var current = (ev.Status ?? "planning").ToLowerInvariant();
        if (current != target && (!Transitions.TryGetValue(current, out var allowed) || !allowed.Contains(target)))
            return ApiResponse<EventResponse>.ErrorResponse($"Cannot transition from '{current}' to '{target}'");

        ev.Status = target;
        ev.SetUpdateAudit(userId);
        _unitOfWork.Events.Update(ev);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<EventResponse>.SuccessResponse(_mapper.Map<EventResponse>(ev), "Status updated");
    }

    public async Task<ApiResponse<bool>> DeleteEventAsync(Guid id, int userId, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByPublicIdAsync(id, ct);
        if (ev == null)
            return ApiResponse<bool>.NotFoundResponse("Event not found");

        ev.MarkAsDeleted(userId);
        _unitOfWork.Events.Update(ev);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.SuccessResponse(true, "Event deleted");
    }

    // ── Event types (admin-managed lookup) ───────────────────────────────────

    public async Task<ApiResponse<List<EventTypeDto>>> GetEventTypesAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.EventTypes.Query()
            .OrderBy(x => x.Name)
            .Select(x => new EventTypeDto { Id = x.PublicId, Name = x.Name })
            .ToListAsync(ct);
        return ApiResponse<List<EventTypeDto>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<EventTypeDto>> CreateEventTypeAsync(CreateEventTypeRequest request, int userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResponse<EventTypeDto>.ErrorResponse("Name is required");

        var entity = new DomainPersistence.Entities.EventType { Name = request.Name.Trim() };
        if (userId != 0) entity.SetCreationAudit(userId);
        await _unitOfWork.EventTypes.AddAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<EventTypeDto>.SuccessResponse(
            new EventTypeDto { Id = entity.PublicId, Name = entity.Name }, "Event type created");
    }

    // ── Sessions ────────────────────────────────────────────────────────────

    public async Task<ApiResponse<List<SessionResponse>>> GetSessionsAsync(Guid eventId, CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.Sessions.Query()
            .Include(s => s.Event)
            .Include(s => s.Venue)
            .Where(s => s.Event.PublicId == eventId)
            .OrderBy(s => s.Date).ThenBy(s => s.Time)
            .ToListAsync(ct);

        return ApiResponse<List<SessionResponse>>.SuccessResponse(_mapper.Map<List<SessionResponse>>(sessions));
    }

    public async Task<ApiResponse<SessionResponse>> AddSessionAsync(Guid eventId, CreateSessionRequest request, int userId, CancellationToken ct = default)
    {
        var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
        if (ev == null)
            return ApiResponse<SessionResponse>.NotFoundResponse("Event not found");
        if (string.IsNullOrWhiteSpace(request.Title))
            return ApiResponse<SessionResponse>.ErrorResponse("Session title is required");

        var dateError = ValidateSessionDate(ev, request.Date);
        if (dateError != null)
            return ApiResponse<SessionResponse>.ErrorResponse(dateError);

        var session = _mapper.Map<Session>(request);
        session.EventId = ev.Id;
        session.Event = ev;

        if (request.VenueId.HasValue)
        {
            var venue = await _unitOfWork.Venues.GetByPublicIdAsync(request.VenueId.Value, ct);
            if (venue == null)
                return ApiResponse<SessionResponse>.NotFoundResponse("Venue not found");
            session.VenueId = venue.Id;
            session.VenueName = venue.Name;
            // Set the nav too: the response is mapped off this instance, and the
            // mapper reads Venue.PublicId.
            session.Venue = venue;
        }

        session.SetCreationAudit(userId);

        await _unitOfWork.Sessions.AddAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<SessionResponse>.SuccessResponse(_mapper.Map<SessionResponse>(session), "Session added");
    }

    public async Task<ApiResponse<SessionResponse>> UpdateSessionAsync(Guid eventId, Guid sessionId, UpdateSessionRequest request, int userId, CancellationToken ct = default)
    {
        var session = await _unitOfWork.Sessions.FindFirstOrDefaultAsync(s => s.PublicId == sessionId && s.Event.PublicId == eventId, ct);
        if (session == null)
            return ApiResponse<SessionResponse>.NotFoundResponse("Session not found");

        var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
        session.Event = ev;
        var dateError = ValidateSessionDate(ev, request.Date ?? session.Date);
        if (dateError != null)
            return ApiResponse<SessionResponse>.ErrorResponse(dateError);

        session.Title = request.Title ?? session.Title;
        session.Date = request.Date ?? session.Date;
        session.Time = request.Time ?? session.Time;

        if (request.VenueId.HasValue)
        {
            var venue = await _unitOfWork.Venues.GetByPublicIdAsync(request.VenueId.Value, ct);
            if (venue == null)
                return ApiResponse<SessionResponse>.NotFoundResponse("Venue not found");
            session.VenueId = venue.Id;
            session.VenueName = venue.Name;
            // Set the nav too: the response is mapped off this instance, and the
            // mapper reads Venue.PublicId.
            session.Venue = venue;
        }
        else
        {
            session.VenueName = request.VenueName ?? session.VenueName;
        }

        session.Room = request.Room ?? session.Room;
        session.Speaker = request.Speaker ?? session.Speaker;
        if (request.Capacity > 0) session.Capacity = request.Capacity;

        session.SetUpdateAudit(userId);
        _unitOfWork.Sessions.Update(session);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<SessionResponse>.SuccessResponse(_mapper.Map<SessionResponse>(session), "Session updated");
    }

    public async Task<ApiResponse<bool>> DeleteSessionAsync(Guid eventId, Guid sessionId, int userId, CancellationToken ct = default)
    {
        var session = await _unitOfWork.Sessions.FindFirstOrDefaultAsync(s => s.PublicId == sessionId && s.Event.PublicId == eventId, ct);
        if (session == null)
            return ApiResponse<bool>.NotFoundResponse("Session not found");

        session.MarkAsDeleted(userId);
        _unitOfWork.Sessions.Update(session);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.SuccessResponse(true, "Session deleted");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    // A session must fall within its event's date range (when both are set).
    private static string ValidateSessionDate(Event ev, DateOnly? date)
    {
        if (ev == null || date is null) return null;
        if (ev.StartDate is { } start && date < start)
            return "Session date can't be before the event start date";
        if (ev.EndDate is { } end && date > end)
            return "Session date can't be after the event end date";
        return null;
    }

    private static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "event";
        var slug = Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "event" : slug;
    }

    private async Task<string> UniqueAppKeyAsync(string baseKey, CancellationToken ct)
    {
        var key = baseKey;
        var i = 2;
        while (await _unitOfWork.Events.AnyAsync(e => e.AppKey == key, ct))
            key = $"{baseKey}-{i++}";
        return key;
    }

    // ── Bulk import ────────────────────────────────────────────────────────

    private const int ImportTemplateRows = 500;
    private static readonly string[] ImportHeaders =
        { "Title*", "Venue", "Type", "Start Date (YYYY-MM-DD)", "End Date (YYYY-MM-DD)", "Image URL" };

    public async Task<byte[]> BuildImportTemplateAsync(CancellationToken ct = default)
    {
        var venueNames = await _unitOfWork.Venues.Query()
            .OrderBy(v => v.Name)
            .Select(v => v.Name)
            .ToListAsync(ct);
        var typeNames = await _unitOfWork.EventTypes.Query()
            .OrderBy(t => t.Name)
            .Select(t => t.Name)
            .ToListAsync(ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Events");

        for (int i = 0; i < ImportHeaders.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = ImportHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#8d0134");
        }
        ws.SheetView.FreezeRows(1);
        ws.Column(1).Width = 32; ws.Column(2).Width = 26; ws.Column(3).Width = 16;
        ws.Column(4).Width = 24; ws.Column(5).Width = 24; ws.Column(6).Width = 34;

        // Hidden helper sheet for the Venue/Type dropdowns' source ranges — a
        // plain inline list caps out around 255 chars, too short once there's
        // more than a handful of rows. Kept in sync with the portal's current
        // Venues/EventTypes every time this template is (re-)exported.
        var listSheet = wb.Worksheets.Add("Lists");
        for (int i = 0; i < venueNames.Count; i++)
            listSheet.Cell(i + 1, 1).Value = venueNames[i];
        for (int i = 0; i < typeNames.Count; i++)
            listSheet.Cell(i + 1, 2).Value = typeNames[i];
        listSheet.Visibility = XLWorksheetVisibility.VeryHidden;

        var venueRange = venueNames.Count > 0 ? $"Lists!$A$1:$A${venueNames.Count}" : null;
        var typeRange = typeNames.Count > 0 ? $"Lists!$B$1:$B${typeNames.Count}" : null;
        var lastRow = ImportTemplateRows;

        // Applied once over the whole range rather than per-cell — same effect,
        // far fewer <dataValidation> entries in the saved file.
        if (venueRange != null)
        {
            var dv = ws.Range(2, 2, lastRow, 2).SetDataValidation();
            dv.List(venueRange, true);
            dv.IgnoreBlanks = true;
            // Stop (not Warning/Information) — without this Excel lets the
            // user type anything and just shows the dropdown as a suggestion,
            // which is how "test" got past the Type column despite it existing.
            dv.ErrorStyle = XLErrorStyle.Stop;
            dv.ErrorTitle = "Invalid Venue";
            dv.ErrorMessage = "Please pick a Venue from the dropdown — typing a value that isn't listed is not allowed.";
        }

        if (typeRange != null)
        {
            var dvType = ws.Range(2, 3, lastRow, 3).SetDataValidation();
            dvType.List(typeRange, true);
            dvType.IgnoreBlanks = true;
            dvType.ErrorStyle = XLErrorStyle.Stop;
            dvType.ErrorTitle = "Invalid Type";
            dvType.ErrorMessage = "Please pick a Type from the dropdown — typing a value that isn't listed is not allowed.";
        }

        // Date validation (not just number formatting) is what makes Excel show
        // the little calendar picker on these cells, and it rejects past dates
        // outright instead of letting the row fail later on import.
        var today = DateTime.Today;
        var dateRange = new[] { 4, 5 };
        foreach (var col in dateRange)
        {
            ws.Column(col).Style.DateFormat.Format = "yyyy-mm-dd";
            var dvDate = ws.Range(2, col, lastRow, col).SetDataValidation();
            dvDate.Date.EqualOrGreaterThan(today);
            dvDate.IgnoreBlanks = true;
            dvDate.ErrorStyle = XLErrorStyle.Stop;
            dvDate.ErrorTitle = "Invalid Date";
            dvDate.ErrorMessage = "Date can't be in the past. Use the calendar picker or type YYYY-MM-DD.";
        }

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }

    // Uploads the file to blob storage (so the background job can re-read it
    // outside the request's lifetime), records an ImportBatch row, and enqueues
    // the actual processing — the HTTP request never waits on the parse/insert
    // work. The caller polls IImportBatchService.GetStatusAsync(batchId).
    public async Task<ApiResponse<StartImportResponse>> StartEventsImportAsync(Stream fileStream, string fileName, int userId, CancellationToken ct = default)
    {
        string fileUrl;
        try
        {
            fileUrl = await _blobService.UploadStreamAsync(fileStream, fileName, "imports", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload events import file");
            return ApiResponse<StartImportResponse>.ServerErrorResponse("Could not upload the file — please try again.");
        }

        var batch = new ImportBatch
        {
            Kind = "events",
            Status = "queued",
            FileUrl = fileUrl,
            CreatedByUserId = userId,
        };
        await _unitOfWork.ImportBatches.AddAsync(batch, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _backgroundJobClient.Enqueue<IEventService>(x => x.ProcessEventsImportBatchAsync(batch.PublicId, CancellationToken.None));

        return ApiResponse<StartImportResponse>.SuccessResponse(
            new StartImportResponse { BatchId = batch.PublicId, Status = batch.Status },
            "Import started — you'll be notified when it's done.");
    }

    // The Hangfire job body — everything ImportEventsAsync used to do
    // synchronously, now writing per-row outcomes to ImportBatchRow instead of
    // an in-memory result, and pushing a notification on completion instead of
    // returning an HTTP response (there's no request left to return one to).
    public async Task ProcessEventsImportBatchAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await _unitOfWork.ImportBatches.Query().FirstOrDefaultAsync(b => b.PublicId == batchId, ct);
        if (batch == null)
        {
            _logger.LogError("Import batch {BatchId} not found", batchId);
            return;
        }

        batch.Status = "processing";
        batch.StartedAt = DateTime.UtcNow;
        _unitOfWork.ImportBatches.Update(batch);
        await _unitOfWork.SaveChangesAsync(ct);

        var rowResults = new List<ImportBatchRow>();
        var toInsert = new List<Event>();

        try
        {
            using var fileStream = await _blobService.DownloadAsync(batch.FileUrl, ct);
            using var wb = new XLWorkbook(fileStream);
            var ws = wb.Worksheets.FirstOrDefault(w => w.Visibility == XLWorksheetVisibility.Visible) ?? wb.Worksheet(1);
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            // Preload everything up front — a bulk import must not do a DB
            // round trip per row, or a few hundred events turns into a few
            // hundred queries.
            var venueLookup = (await _unitOfWork.Venues.Query()
                    .Select(v => new { v.Id, v.Name })
                    .ToListAsync(ct))
                .GroupBy(v => v.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            var existingAppKeys = new HashSet<string>(
                await _unitOfWork.Events.Query().Select(e => e.AppKey).ToListAsync(ct),
                StringComparer.OrdinalIgnoreCase);

            var validTypes = new HashSet<string>(
                await _unitOfWork.EventTypes.Query().Select(t => t.Name).ToListAsync(ct),
                StringComparer.OrdinalIgnoreCase);

            int? creatorId = batch.CreatedByUserId == 0 ? null : batch.CreatedByUserId;

            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                var title = row.Cell(1).GetString().Trim();
                var venueName = row.Cell(2).GetString().Trim();
                var type = row.Cell(3).GetString().Trim();
                var startCell = row.Cell(4);
                var endCell = row.Cell(5);
                var imageUrl = row.Cell(6).GetString().Trim();

                var rowIsBlank = title.Length == 0 && venueName.Length == 0 && type.Length == 0
                    && startCell.IsEmpty() && endCell.IsEmpty() && imageUrl.Length == 0;
                if (rowIsBlank) continue;

                void Fail(string error, string category) => rowResults.Add(new ImportBatchRow
                { RowNumber = r, Title = title, Success = false, Error = error, ErrorCategory = category });

                if (string.IsNullOrWhiteSpace(title)) { Fail("Title is required.", "validation"); continue; }

                int? venueId = null;
                if (!string.IsNullOrWhiteSpace(venueName))
                {
                    if (venueLookup.TryGetValue(venueName, out var vid)) venueId = vid;
                    else { Fail($"Venue \"{venueName}\" no longer exists in the portal.", "stale_venue"); continue; }
                }

                if (!string.IsNullOrWhiteSpace(type) && !validTypes.Contains(type))
                { Fail($"Type \"{type}\" is not a recognized event type.", "stale_type"); continue; }

                if (!TryReadDate(startCell, out var startDate)) { Fail("Start Date is not a valid date.", "validation"); continue; }
                if (!TryReadDate(endCell, out var endDate)) { Fail("End Date is not a valid date.", "validation"); continue; }
                if (startDate.HasValue && endDate.HasValue && endDate < startDate)
                { Fail("End Date can't be before Start Date.", "validation"); continue; }

                var baseKey = Slugify(title);
                var appKey = baseKey;
                var suffix = 2;
                while (existingAppKeys.Contains(appKey)) appKey = $"{baseKey}-{suffix++}";
                existingAppKeys.Add(appKey);

                var ev = new Event
                {
                    Title = title,
                    Type = string.IsNullOrWhiteSpace(type) ? null : type,
                    VenueName = string.IsNullOrWhiteSpace(venueName) ? null : venueName,
                    VenueId = venueId,
                    StartDate = startDate,
                    EndDate = endDate,
                    ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl,
                    Status = "planning",
                    AppKey = appKey,
                };
                if (creatorId.HasValue) ev.SetCreationAudit(creatorId.Value);

                toInsert.Add(ev);
                rowResults.Add(new ImportBatchRow { RowNumber = r, Title = title, Success = true });
            }

            if (toInsert.Count > 0)
                await _unitOfWork.Events.AddRangeAsync(toInsert, ct);

            foreach (var rr in rowResults) rr.ImportBatchId = batch.Id;
            if (rowResults.Count > 0)
                await _unitOfWork.ImportBatchRows.AddRangeAsync(rowResults, ct);

            batch.Total = rowResults.Count;
            batch.Imported = toInsert.Count;
            batch.Failed = rowResults.Count(x => !x.Success);
            batch.Status = "completed";
            batch.CompletedAt = DateTime.UtcNow;
            _unitOfWork.ImportBatches.Update(batch);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing events import batch {BatchId}", batchId);
            batch.Status = "failed";
            batch.ErrorMessage = "Failed to read the file — make sure it's a .xlsx exported from the template.";
            batch.CompletedAt = DateTime.UtcNow;
            _unitOfWork.ImportBatches.Update(batch);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        await _importBatchService.NotifyFinishedAsync(batch, "Events", "/events", ct);
    }

    // Accepts a real Excel date/datetime cell, or a text cell in a format
    // .NET can parse (e.g. the "yyyy-mm-dd" the template formats cells as).
    // An empty cell is valid — Start/End Date are optional.
    private static bool TryReadDate(IXLCell cell, out DateOnly? date)
    {
        date = null;
        if (cell.IsEmpty()) return true;

        if (cell.DataType == XLDataType.DateTime)
        {
            date = DateOnly.FromDateTime(cell.GetDateTime());
            return true;
        }

        var s = cell.GetString().Trim();
        if (s.Length == 0) return true;

        if (DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            date = parsed;
            return true;
        }
        return false;
    }
}
