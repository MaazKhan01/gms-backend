using AutoMapper;
using Core.Common;
using Core.Common.Interfaces;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Guest;
using Core.ViewModel.Invitation;
using ClosedXML.Excel;
using DomainPersistence.Entities;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Infrastructure.Services;

// Core guest CRUD, plus sending/resending the guest-invitation email — the
// Invitation entity itself (token/status/accreditation) lives in its own
// table (see InvitationService for the public accept/respond side).
public class GuestService(
    IUnitOfWork _unitOfWork,
    IMapper _mapper,
    IEmailService _emailService,
    IConfiguration _configuration,
    ILogger<GuestService> _logger,
    IBlobService _blobService,
    IBackgroundJobClient _backgroundJobClient,
    IImportBatchService _importBatchService,
    ICurrentUser _currentUser) : IGuestService
{
    private string FrontendUrl => _configuration.GetValue<string>("FrontendUrl") ?? "http://localhost:5173";

    // Auto-provisions the User row every Guest is now 1:1 with (see Guest.UserId
    // remarks) — RoleId -> the "guest" role (PortalAccess=false, no permissions),
    // PasswordHash left null (OTP via CurrentGuest/VipAppService remains the only
    // way in). Email/UserName are deliberately left null: Guests.Email has no
    // uniqueness constraint (the same person can be re-invited per event), which
    // would collide with Users' filtered-unique Email index.
    private async Task<User> CreateLinkedUserAsync(string firstName, string lastName, CancellationToken ct)
    {
        var guestRole = await _unitOfWork.Roles.FindFirstOrDefaultAsync(r => r.Code == Roles.GUEST, ct);
        var user = new User
        {
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            RoleId = guestRole?.Id,
        };
        await _unitOfWork.Users.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct); // need the generated Id for Guest.UserId
        return user;
    }

    // Upserts the guest's Invitation row (one per guest) with a fresh token
    // and fires the branded email. Called from Create/UpdateGuestAsync when
    // an InvitationTemplateId is supplied — including to resend.
    private async Task SendInvitationAsync(EventGuest guest, Guid templateId, CancellationToken ct)
    {
        var template = await _unitOfWork.InvitationTemplates.Query()
            .FirstOrDefaultAsync(t => t.PublicId == templateId, ct);
        if (template == null) return;

        var invitation = await _unitOfWork.Invitations.Query()
            .FirstOrDefaultAsync(i => i.EventGuestId == guest.Id, ct);

        if (invitation == null)
        {
            invitation = new Invitation { EventGuestId = guest.Id, AccreditationStatus = GuestAccreditationStatus.NotIssued };
            await _unitOfWork.Invitations.AddAsync(invitation, ct);
        }

        if (invitation.InvitationToken == null || invitation.InvitationToken == Guid.Empty)
            invitation.InvitationToken = Guid.NewGuid();

        invitation.InvitationTemplateId = template.Id;
        invitation.InvitationStatus = GuestInvitationStatus.Sent;
        invitation.SentAt = DateTime.UtcNow;
        // Tracked either way (Added above, or loaded by tracking Query) — no Update() call:
        // it would flip the new row Added -> Modified while Id is still a temp value and throw.
        await _unitOfWork.SaveChangesAsync(ct);

        var ev = await _unitOfWork.Events.Query().FirstOrDefaultAsync(e => e.Id == guest.EventId, ct);
        var guestName = $"{guest.Guest.FirstName} {guest.Guest.LastName}".Trim();
        var link = $"{FrontendUrl}/?screen=invitation&token={invitation.InvitationToken}";

        // If the admin placed their own invite button in the body (via the
        // {{InviteLink}} placeholder), we swap in the real URL and suppress the
        // email shell's default CTA so there aren't two buttons. Otherwise the
        // shell appends its standard "View Invitation & Respond" button (as before).
        var rawBody = template.Body ?? "";
        var hasOwnButton = rawBody.Contains("{{InviteLink}}");
        var emailBody = rawBody
            .Replace("{{GuestName}}", guestName)
            .Replace("{{FirstName}}", guest.Guest.FirstName)
            .Replace("{{LastName}}", guest.Guest.LastName)
            .Replace("{{EventName}}", ev?.Title ?? "")
            .Replace("{{EventDate}}", ev?.StartDate?.ToString("dd MMM yyyy") ?? "")
            .Replace("{{Venue}}", ev?.VenueName ?? "")
            .Replace("{{InviteLink}}", link);

        var emailModel = new GuestInvitationEmailModel
        {
            GuestName = guestName,
            Subject = template.Subject,
            BodyHtml = emailBody,
            CtaUrl = hasOwnButton ? null : link,
            EventTitle = ev?.Title,
            EventVenue = ev?.VenueName,
            EventStartDate = ev?.StartDate,
            EventEndDate = ev?.EndDate,
            Tier = guest.ServiceLevel?.Name,
            Reference = invitation.InvitationToken?.ToString("N")[..8].ToUpperInvariant(),
        };

        var email = guest.Guest.Email;
        _ = Task.Run(async () =>
        {
            try { await _emailService.SendGuestInvitationAsync(email, emailModel); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not send invitation email to {Email}", email); }
        });
    }
    // The full participation graph GuestResponse maps from: the person (plus their
    // nationality), the event, the level, and this event's session picks. One
    // definition so every read path returns an identically-shaped object.
    private IQueryable<EventGuest> LoadEventGuestGraph()
        => _unitOfWork.EventGuests.Query()
            .Include(g => g.Guest).ThenInclude(p => p.Nationality)
            .Include(g => g.GuestSessions).ThenInclude(gs => gs.Session)
            .Include(g => g.OrganizationRef)
            .Include(g => g.ServiceLevel)
            .Include(g => g.Event);

    public async Task<ApiResponse<GuestResponse>> GetGuestByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var guest = await LoadEventGuestGraph().FirstOrDefaultAsync(g => g.PublicId == id, ct);

            if (guest == null)
                return ApiResponse<GuestResponse>.NotFoundResponse("Guest not found");

            var response = _mapper.Map<GuestResponse>(guest);
            await MergeInvitationAsync(response, guest.Id, ct);
            return ApiResponse<GuestResponse>.SuccessResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving guest {GuestId}", id);
            return ApiResponse<GuestResponse>.ServerErrorResponse("An error occurred while retrieving the guest");
        }
    }

    // Merges the guest's Invitation row (status/accreditation/template) into an
    // already-mapped GuestResponse — Invitation isn't part of the Guest entity
    // graph, so AutoMapper can't reach it on its own.
    private async Task MergeInvitationAsync(GuestResponse response, int guestId, CancellationToken ct)
    {
        var invitation = await _unitOfWork.Invitations.Query()
            .Include(i => i.InvitationTemplate)
            .FirstOrDefaultAsync(i => i.EventGuestId == guestId, ct);

        response.InvitationStatus = invitation?.InvitationStatus ?? GuestInvitationStatus.NotSent;
        response.AccreditationStatus = invitation?.AccreditationStatus ?? GuestAccreditationStatus.NotIssued;
        response.InvitationTemplateId = invitation?.InvitationTemplate?.PublicId;
    }

    private async Task MergeInvitationsAsync(List<GuestResponse> responses, List<int> guestIds, CancellationToken ct)
    {
        if (guestIds.Count == 0) return;

        var invitations = await _unitOfWork.Invitations.Query()
            .Include(i => i.InvitationTemplate)
            .Where(i => guestIds.Contains(i.EventGuestId))
            .ToListAsync(ct);
        var byGuestId = invitations.ToDictionary(i => i.EventGuestId, i => i);

        for (var idx = 0; idx < responses.Count && idx < guestIds.Count; idx++)
        {
            byGuestId.TryGetValue(guestIds[idx], out var invitation);
            responses[idx].InvitationStatus = invitation?.InvitationStatus ?? GuestInvitationStatus.NotSent;
            responses[idx].AccreditationStatus = invitation?.AccreditationStatus ?? GuestAccreditationStatus.NotIssued;
            responses[idx].InvitationTemplateId = invitation?.InvitationTemplate?.PublicId;
        }
    }

    public async Task<ApiResponse<bool>> BulkGuestsDeleteAsync(Guid eventId, DeleteMultipleGuests request, CancellationToken ct = default)
    {
        try
        {
            var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
            if (ev == null)
                return ApiResponse<bool>.NotFoundResponse("Event not found");

            var guests = await _unitOfWork.EventGuests
                .Query()
                .Where(g => request.SelectedGuestsToDelete.Contains(g.PublicId) && g.EventId == ev.Id)
                .ToListAsync(ct);

            if (!guests.Any())
                return ApiResponse<bool>.NotFoundResponse("No matching guests found");

            // Same Restrict-FK concern as DeleteGuestByIdAsync — free any seats
            // held by these guests before removing them.
            var guestIds = guests.Select(g => g.Id).ToList();
            var seatAssigns = await _unitOfWork.SeatAssigns.Query()
                .Where(sa => guestIds.Contains(sa.EventGuestId))
                .ToListAsync(ct);
            if (seatAssigns.Count > 0)
                _unitOfWork.SeatAssigns.RemoveRange(seatAssigns);

            _unitOfWork.EventGuests.RemoveRange(guests);
            await _unitOfWork.SaveChangesAsync(ct);
            return ApiResponse<bool>.SuccessResponse(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk-deleting guests for event {EventId}", eventId);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting guests");
        }
    }

    // Uploads the CSV to blob storage (so the background job can re-read it
    // outside the request's lifetime), records an ImportBatch row, and enqueues
    // the actual processing — the HTTP request never waits on it. The caller
    // polls IImportBatchService.GetStatusAsync(batchId).
    public async Task<ApiResponse<StartImportResponse>> StartGuestsImportAsync(Guid eventId, Stream csvStream, string fileName, int createdBy, CancellationToken ct)
    {
        string fileUrl;
        try
        {
            fileUrl = await _blobService.UploadStreamAsync(csvStream, fileName, "imports", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload guests import file");
            return ApiResponse<StartImportResponse>.ServerErrorResponse("Could not upload the file — please try again.");
        }

        var eventEntity = await _unitOfWork.Events.Query().FirstOrDefaultAsync(e => e.PublicId == eventId, ct);
        if (eventEntity == null)
            return ApiResponse<StartImportResponse>.NotFoundResponse("Event not found");

        var batch = new ImportBatch
        {
            Kind = "guests",
            EventId = eventEntity.Id,
            Status = "queued",
            FileUrl = fileUrl,
            CreatedByUserId = createdBy,
        };
        await _unitOfWork.ImportBatches.AddAsync(batch, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _backgroundJobClient.Enqueue<IGuestService>(x => x.ProcessGuestsImportBatchAsync(batch.PublicId, CancellationToken.None));

        return ApiResponse<StartImportResponse>.SuccessResponse(
            new StartImportResponse { BatchId = batch.PublicId, Status = batch.Status },
            "Import started — you'll be notified when it's done.");
    }

    // A guest's booked stay can start a bit before the event and run a bit
    // after it — same slack the guest wizard's own Arrival/Departure fields
    // use (GuestModal.jsx DATE_MARGIN_DAYS), so the template's date validation
    // agrees with what the UI would accept.
    private const int GuestImportDateMarginDays = 7;

    // The header row IS the version check: an older template (still carrying
    // a "Tier" column, no "Service Level") or a hand-edited file fails right
    // here with one clear message, instead of every row quietly reading from
    // the wrong column. Keep this in lockstep with BuildGuestImportTemplateAsync.
    private static readonly string[] GuestImportHeaders =
    {
        "First Name*", "Last Name*", "Email*", "Guest Type", "Organization",
        "Nationality", "Service Level", "Arrival Date (YYYY-MM-DD)",
        "Departure Date (YYYY-MM-DD)", "Accreditation Required",
    };

    // Same shape the guest wizard uses (GuestModal.jsx handleNext) — kept in
    // lockstep so a row that would fail the wizard's own validation fails the
    // same way here instead of silently creating an emailless guest.
    private static readonly System.Text.RegularExpressions.Regex GuestImportEmailPattern =
        new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    // The Hangfire job body — reads the .xlsx template back, resolving every
    // dropdown column against what currently exists (so a value that existed
    // when the template was downloaded but was since renamed/deleted fails
    // that one row with a clear reason), then calls CreateGuestAsync per row.
    public async Task ProcessGuestsImportBatchAsync(Guid batchId, CancellationToken ct = default)
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

        var eventEntity = await _unitOfWork.Events.Query().FirstOrDefaultAsync(e => e.Id == batch.EventId, ct);
        var rowResults = new List<ImportBatchRow>();

        try
        {
            using var fileStream = await _blobService.DownloadAsync(batch.FileUrl, ct);
            using var wb = new XLWorkbook(fileStream);
            var ws = wb.Worksheets.FirstOrDefault(w => w.Visibility == XLWorksheetVisibility.Visible) ?? wb.Worksheet(1);

            var actualHeaders = Enumerable.Range(1, GuestImportHeaders.Length)
                .Select(i => ws.Row(1).Cell(i).GetString().Trim())
                .ToArray();
            if (!actualHeaders.SequenceEqual(GuestImportHeaders, StringComparer.OrdinalIgnoreCase))
            {
                batch.Status = "failed";
                batch.ErrorMessage = "This file doesn't match the current import template — please download a fresh template and try again.";
                batch.CompletedAt = DateTime.UtcNow;
                _unitOfWork.ImportBatches.Update(batch);
                await _unitOfWork.SaveChangesAsync(ct);
                return;
            }

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            var guestTypeByName = GuestEnumCatalog.All[GuestEnumCatalog.Type]
                .ToDictionary(o => o.Name, o => o.Code, StringComparer.OrdinalIgnoreCase);
            var orgByName = (await _unitOfWork.Organizations.Query()
                    .Select(o => new { o.PublicId, o.Name })
                    .ToListAsync(ct))
                .GroupBy(o => o.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().PublicId, StringComparer.OrdinalIgnoreCase);
            var levelByName = (await _unitOfWork.ServiceLevels.Query()
                    .Where(l => l.IsActive)
                    .Select(l => new { l.PublicId, l.Name })
                    .ToListAsync(ct))
                .GroupBy(l => l.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().PublicId, StringComparer.OrdinalIgnoreCase);

            var minDate = eventEntity?.StartDate?.AddDays(-GuestImportDateMarginDays);
            var maxDate = eventEntity?.EndDate?.AddDays(GuestImportDateMarginDays);

            for (var r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                var firstName = row.Cell(1).GetString().Trim();
                var lastName = row.Cell(2).GetString().Trim();
                var email = row.Cell(3).GetString().Trim();
                var guestTypeName = row.Cell(4).GetString().Trim();
                var orgName = row.Cell(5).GetString().Trim();
                var nationalityName = row.Cell(6).GetString().Trim();
                var levelName = row.Cell(7).GetString().Trim();
                var arrivalCell = row.Cell(8);
                var departureCell = row.Cell(9);
                var accredText = row.Cell(10).GetString().Trim();

                var rowIsBlank = firstName.Length == 0 && lastName.Length == 0 && email.Length == 0
                    && guestTypeName.Length == 0 && orgName.Length == 0 && nationalityName.Length == 0
                    && levelName.Length == 0 && arrivalCell.IsEmpty() && departureCell.IsEmpty() && accredText.Length == 0;
                if (rowIsBlank) continue;

                var name = $"{firstName} {lastName}".Trim();
                void Fail(string error) => rowResults.Add(new ImportBatchRow
                {
                    RowNumber = r, Title = name.Length > 0 ? name : email, Success = false,
                    Error = error, ErrorCategory = "validation",
                });

                if (firstName.Length == 0 || lastName.Length == 0) { Fail("Missing First or Last Name."); continue; }
                if (email.Length == 0) { Fail("Missing Email."); continue; }
                if (!GuestImportEmailPattern.IsMatch(email)) { Fail($"\"{email}\" is not a valid email address."); continue; }

                var guestTypeCode = "delegate";
                if (guestTypeName.Length > 0 && !guestTypeByName.TryGetValue(guestTypeName, out guestTypeCode))
                { Fail($"\"{guestTypeName}\" is not a recognized Guest Type — pick one from the dropdown."); continue; }

                Guid? organizationId = null;
                if (orgName.Length > 0)
                {
                    if (!orgByName.TryGetValue(orgName, out var oid))
                    { Fail($"Organization \"{orgName}\" was not found — pick one from the dropdown, or leave it blank."); continue; }
                    organizationId = oid;
                }

                Guid? serviceLevelId = null;
                if (levelName.Length > 0)
                {
                    if (!levelByName.TryGetValue(levelName, out var lid))
                    { Fail($"Service Level \"{levelName}\" was not found — pick one from the dropdown, or leave it blank."); continue; }
                    serviceLevelId = lid;
                }

                if (!TryReadDateCell(arrivalCell, out var arrivalDate)) { Fail("Arrival Date is not a valid date."); continue; }
                if (!TryReadDateCell(departureCell, out var departureDate)) { Fail("Departure Date is not a valid date."); continue; }
                if (arrivalDate.HasValue && ((minDate.HasValue && arrivalDate < minDate) || (maxDate.HasValue && arrivalDate > maxDate)))
                { Fail($"Arrival Date must be between {minDate:yyyy-MM-dd} and {maxDate:yyyy-MM-dd}."); continue; }
                if (departureDate.HasValue && ((minDate.HasValue && departureDate < minDate) || (maxDate.HasValue && departureDate > maxDate)))
                { Fail($"Departure Date must be between {minDate:yyyy-MM-dd} and {maxDate:yyyy-MM-dd}."); continue; }
                if (arrivalDate.HasValue && departureDate.HasValue && departureDate < arrivalDate)
                { Fail("Departure Date can't be before Arrival Date."); continue; }

                try
                {
                    var request = new CreateGuestRequest
                    {
                        FirstName = firstName,
                        LastName = lastName,
                        Email = email.Length > 0 ? email : null,
                        EventId = eventEntity.PublicId,
                        GuestType = guestTypeCode,
                        OrganizationId = organizationId,
                        NationalityId = await ResolveNationalityByNameAsync(nationalityName, ct),
                        ServiceLevelId = serviceLevelId,
                        AccreditationRequired = ParseCsvBool(accredText),
                    };
                    var createResult = await CreateGuestAsync(request, ct);
                    rowResults.Add(new ImportBatchRow
                    {
                        RowNumber = r, Title = name, Success = createResult.Success,
                        Error = createResult.Success ? null : createResult.Message,
                        ErrorCategory = createResult.Success ? null : "validation",
                    });
                }
                catch (Exception ex)
                {
                    Fail(ex.Message);
                }
            }

            foreach (var rr in rowResults) rr.ImportBatchId = batch.Id;
            if (rowResults.Count > 0)
                await _unitOfWork.ImportBatchRows.AddRangeAsync(rowResults, ct);

            batch.Total = rowResults.Count;
            batch.Imported = rowResults.Count(x => x.Success);
            batch.Failed = rowResults.Count(x => !x.Success);
            batch.Status = "completed";
            batch.CompletedAt = DateTime.UtcNow;
            _unitOfWork.ImportBatches.Update(batch);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing guests import batch {BatchId}", batchId);
            batch.Status = "failed";
            batch.ErrorMessage = "The uploaded file isn't a valid .xlsx import — make sure it's the template downloaded from this portal.";
            batch.CompletedAt = DateTime.UtcNow;
            _unitOfWork.ImportBatches.Update(batch);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        await _importBatchService.NotifyFinishedAsync(batch, "Guests", "/guests", ct);
    }

    // Picker feed: one projected query, no includes, no AutoMapper, no invitation
    // merge — just the five columns a dropdown row draws. Declined guests are
    // always filtered out; a picker never wants them.
    public async Task<ApiResponse<PaginatedResponse<GuestPickerResponse>>> GetGuestPickerAsync(
        Guid eventId, PagedRequest request, CancellationToken ct = default)
    {
        try
        {
            var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
            if (ev == null)
                return ApiResponse<PaginatedResponse<GuestPickerResponse>>.NotFoundResponse("Event not found");

            var query = _unitOfWork.EventGuests.QueryNoTracking().Where(g => g.EventId == ev.Id);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.Trim();
                query = query.Where(g =>
                    g.Guest.FirstName.Contains(term) ||
                    g.Guest.LastName.Contains(term) ||
                    (g.Organization != null && g.Organization.Contains(term)));
            }

            query = query.Where(g => !_unitOfWork.Invitations.Query()
                .Any(i => i.EventGuestId == g.Id && i.InvitationStatus == GuestInvitationStatus.Declined));

            var total = await query.CountAsync(ct);

            var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;
            var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;

            var items = await query
                .OrderBy(g => g.Guest.FirstName).ThenBy(g => g.Guest.LastName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(g => new GuestPickerResponse
                {
                    Id = g.PublicId,
                    FullName = (g.Guest.FirstName + " " + g.Guest.LastName).Trim(),
                    Email = g.Guest.Email,
                    Organization = g.Organization,
                    Tier = g.ServiceLevel != null ? g.ServiceLevel.Name : null,
                    PhotoUrl = g.Guest.PhotoUrl,
                })
                .ToListAsync(ct);

            return ApiResponse<PaginatedResponse<GuestPickerResponse>>.SuccessResponse(
                new PaginatedResponse<GuestPickerResponse>(items, total, pageNumber, pageSize));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving guest picker for event {EventId}", eventId);
            return ApiResponse<PaginatedResponse<GuestPickerResponse>>.ServerErrorResponse("An error occurred while retrieving guests");
        }
    }

    public async Task<ApiResponse<PaginatedResponse<OtherEventGuestRow>>> GetGuestsFromOtherEventsAsync(
        Guid currentEventId, PagedRequest request, CancellationToken ct = default)
    {
        try
        {
            var ev = await _unitOfWork.Events.GetByPublicIdAsync(currentEventId, ct);
            if (ev == null)
                return ApiResponse<PaginatedResponse<OtherEventGuestRow>>.NotFoundResponse("Event not found");

            var query = _unitOfWork.EventGuests.QueryNoTracking().Where(g => g.EventId != ev.Id);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.Trim();
                query = query.Where(g =>
                    g.Guest.FirstName.Contains(term) ||
                    g.Guest.LastName.Contains(term) ||
                    (g.Guest.Email != null && g.Guest.Email.Contains(term)) ||
                    (g.Organization != null && g.Organization.Contains(term)) ||
                    (g.OrganizationRef != null && g.OrganizationRef.Name.Contains(term)));
            }

            var total = await query.CountAsync(ct);

            var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;
            var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;

            var raw = await query
                .OrderBy(g => g.Guest.FirstName).ThenBy(g => g.Guest.LastName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(g => new
                {
                    GuestId = g.Id,
                    Row = new OtherEventGuestRow
                    {
                        Id = g.PublicId,
                        FirstName = g.Guest.FirstName,
                        LastName = g.Guest.LastName,
                        Email = g.Guest.Email,
                        GuestType = g.GuestType,
                        OrganizationId = g.OrganizationRef != null ? (Guid?)g.OrganizationRef.PublicId : null,
                        OrganizationName = g.OrganizationRef != null ? g.OrganizationRef.Name : g.Organization,
                        NationalityId = g.Guest.Nationality != null ? (Guid?)g.Guest.Nationality.PublicId : null,
                        NationalityName = g.Guest.Nationality != null ? g.Guest.Nationality.Name : null,
                        NationalityFlag = g.Guest.Nationality != null ? g.Guest.Nationality.Flag : null,
                        PhotoUrl = g.Guest.PhotoUrl,
                        Tier = g.ServiceLevel != null ? g.ServiceLevel.Name : null,
                        ServiceLevelId = g.ServiceLevel != null ? (Guid?)g.ServiceLevel.PublicId : null,
                        ServiceLevelName = g.ServiceLevel != null ? g.ServiceLevel.Name : null,
                        ServiceLevelColor = g.ServiceLevel != null ? g.ServiceLevel.Color : null,
                        AccreditationRequired = g.AccreditationRequired,
                        EventId = g.Event.PublicId,
                        EventTitle = g.Event.Title,
                    },
                })
                .ToListAsync(ct);

            var items = raw.Select(r => r.Row).ToList();
            var guestIds = raw.Select(r => r.GuestId).ToList();

            if (guestIds.Count > 0)
            {
                var invitations = await _unitOfWork.Invitations.Query()
                    .Where(i => guestIds.Contains(i.EventGuestId))
                    .ToListAsync(ct);
                var byGuestId = invitations.ToDictionary(i => i.EventGuestId, i => i);

                for (var idx = 0; idx < items.Count; idx++)
                {
                    byGuestId.TryGetValue(guestIds[idx], out var invitation);
                    items[idx].InvitationStatus = invitation?.InvitationStatus ?? GuestInvitationStatus.NotSent;
                    items[idx].AccreditationStatus = invitation?.AccreditationStatus ?? GuestAccreditationStatus.NotIssued;
                }
            }

            return ApiResponse<PaginatedResponse<OtherEventGuestRow>>.SuccessResponse(
                new PaginatedResponse<OtherEventGuestRow>(items, total, pageNumber, pageSize));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving guests from other events for event {EventId}", currentEventId);
            return ApiResponse<PaginatedResponse<OtherEventGuestRow>>.ServerErrorResponse("An error occurred while retrieving guests");
        }
    }

    public async Task<ApiResponse<PaginatedResponse<GuestResponse>>> GetGuestsAsync(Guid eventId, GuestPagedRequest request, CancellationToken ct = default)
    {
        try
        {
            var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
            if (ev == null)
                return ApiResponse<PaginatedResponse<GuestResponse>>.NotFoundResponse("Event not found");

            var query = LoadEventGuestGraph().Where(g => g.EventId == ev.Id);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLower();
                query = query.Where(g =>
                    g.Guest.FirstName.ToLower().Contains(term) ||
                    g.Guest.LastName.ToLower().Contains(term) ||
                    (g.Guest.Email != null && g.Guest.Email.ToLower().Contains(term)) ||
                    (g.Organization != null && g.Organization.ToLower().Contains(term)));
            }

            // Legacy `tier` filter, now matched against the Service Level's name —
            // Guest.Tier is gone (grades are Service Levels).
            if (!string.IsNullOrWhiteSpace(request.Tier))
            {
                var tier = request.Tier.ToLower();
                query = query.Where(g => g.ServiceLevel != null && g.ServiceLevel.Name.ToLower() == tier);
            }

            if (request.ServiceLevelId is { } levelPublicId && levelPublicId != Guid.Empty)
            {
                var level = await _unitOfWork.ServiceLevels.QueryNoTracking()
                    .FirstOrDefaultAsync(l => l.PublicId == levelPublicId, ct);
                // Unknown id must return nothing, not everything.
                query = level == null
                    ? query.Where(_ => false)
                    : query.Where(g => g.ServiceLevelId == level.Id);
            }

            // Invitation status lives on the Invitation row, not the Guest — and
            // "not_sent" also covers guests who have no invitation row at all.
            if (!string.IsNullOrWhiteSpace(request.InvitationStatus))
            {
                var status = request.InvitationStatus;
                query = status == GuestInvitationStatus.NotSent
                    ? query.Where(g => !_unitOfWork.Invitations.Query()
                        .Any(i => i.EventGuestId == g.Id && i.InvitationStatus != GuestInvitationStatus.NotSent))
                    : query.Where(g => _unitOfWork.Invitations.Query()
                        .Any(i => i.EventGuestId == g.Id && i.InvitationStatus == status));
            }

            // Multi-select variant used by the Guests filter panel — same rules
            // as above, OR'd across every selected status.
            if (!string.IsNullOrWhiteSpace(request.InvitationStatuses))
            {
                var statuses = request.InvitationStatuses
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
                if (statuses.Count > 0)
                {
                    var includesNotSent = statuses.Contains(GuestInvitationStatus.NotSent);
                    var otherStatuses = statuses.Where(s => s != GuestInvitationStatus.NotSent).ToList();
                    query = query.Where(g =>
                        (includesNotSent && !_unitOfWork.Invitations.Query()
                            .Any(i => i.EventGuestId == g.Id && i.InvitationStatus != GuestInvitationStatus.NotSent))
                        || (otherStatuses.Count > 0 && _unitOfWork.Invitations.Query()
                            .Any(i => i.EventGuestId == g.Id && otherStatuses.Contains(i.InvitationStatus))));
                }
            }

            if (request.OrganizationId.HasValue && request.OrganizationId != Guid.Empty)
            {
                var org = await _unitOfWork.Organizations.GetByPublicIdAsync(request.OrganizationId.Value, ct);
                query = query.Where(g => org != null && g.OrganizationId == org.Id);
            }

            if (request.NationalityId.HasValue && request.NationalityId != Guid.Empty)
            {
                var nat = await _unitOfWork.Nationalities.GetByPublicIdAsync(request.NationalityId.Value, ct);
                query = query.Where(g => nat != null && g.Guest.NationalityId == nat.Id);
            }

            // Accreditation: "not_required" (flag off) / "pending" (flag on, not
            // yet issued) / "issued" (flag on, Invitation.AccreditationStatus == Issued).
            if (!string.IsNullOrWhiteSpace(request.AccreditationStatus))
            {
                if (request.AccreditationStatus == "not_required")
                    query = query.Where(g => !g.AccreditationRequired);
                else if (request.AccreditationStatus == GuestAccreditationStatus.Issued)
                    query = query.Where(g => g.AccreditationRequired && _unitOfWork.Invitations.Query()
                        .Any(i => i.EventGuestId == g.Id && i.AccreditationStatus == GuestAccreditationStatus.Issued));
                else if (request.AccreditationStatus == "pending")
                    query = query.Where(g => g.AccreditationRequired && !_unitOfWork.Invitations.Query()
                        .Any(i => i.EventGuestId == g.Id && i.AccreditationStatus == GuestAccreditationStatus.Issued));
            }

            // Downstream pickers (seating/meetings/travel) pass excludeDeclined=true
            // so a guest who rejected their invitation can't be assigned anywhere.
            if (request.ExcludeDeclined)
            {
                query = query.Where(g => !_unitOfWork.Invitations.Query()
                    .Any(i => i.EventGuestId == g.Id && i.InvitationStatus == GuestInvitationStatus.Declined));
            }

            var total = await query.CountAsync(ct);

            var guests = await query
                .OrderBy(g => g.Guest.FirstName)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(ct);

            var mapped = _mapper.Map<List<GuestResponse>>(guests);
            await MergeInvitationsAsync(mapped, guests.Select(g => g.Id).ToList(), ct);

            var paged = new PaginatedResponse<GuestResponse>(
                mapped, total, request.PageNumber, request.PageSize);

            return ApiResponse<PaginatedResponse<GuestResponse>>.SuccessResponse(paged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving guests for event {EventId}", eventId);
            return ApiResponse<PaginatedResponse<GuestResponse>>.ServerErrorResponse("An error occurred while retrieving guests");
        }
    }

    public async Task<ApiResponse<bool>> DeleteGuestByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var guest = await _unitOfWork.Guests.FindFirstOrDefaultAsync(g => g.PublicId == id);
            if (guest == null)
                return ApiResponse<bool>.NotFoundResponse("Guest Not Found");

            // SeatAssign.GuestId is a Restrict FK — free any seat(s) this guest holds
            // first so the delete doesn't fail, and the seat becomes assignable again.
            var seatAssigns = await _unitOfWork.SeatAssigns.Query()
                .Where(sa => sa.EventGuestId == guest.Id)
                .ToListAsync(ct);
            if (seatAssigns.Count > 0)
                _unitOfWork.SeatAssigns.RemoveRange(seatAssigns);

            _unitOfWork.Guests.Remove(guest);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "Guest deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting guest {GuestId}", id);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the guest");
        }
    }

    public async Task<ApiResponse<GuestResponse>> UpdateGuestAsync(CreateGuestRequest request, CancellationToken ct)
    {
        try
        {
            if (request.Id == null || request.Id == Guid.Empty)
                return ApiResponse<GuestResponse>.ErrorResponse("Guest Id is required");

            var guest = await _unitOfWork.EventGuests.Query()
                .Include(g => g.Guest).ThenInclude(p => p.User)
                .Include(g => g.GuestSessions)
                .Include(g => g.OrganizationRef)
                .Include(g => g.ServiceLevel)
                .Include(g => g.Event)
                .FirstOrDefaultAsync(g => g.PublicId == request.Id.Value, ct);

            if (guest == null)
                return ApiResponse<GuestResponse>.NotFoundResponse("Guest not found");

            // Service levels apply to BOTH guest models now (docs/service-levels-v2.md
            // §4): the model decides how the level's services must be COMPLETED —
            // mandatory and in order on fixed, optional and in any order on flexible
            // — not whether a level is assigned at all. Assigning it on flexible
            // events too is what gives those guests a service checklist; skipping it
            // left ServiceLevelId null and the guest with no services.
            var usesLevels = EventGuestModels.UsesServiceLevels(guest.Event?.GuestModel);

            var (serviceLevel, levelError) =
                await ResolveServiceLevelAsync(request.ServiceLevelId, guest.EventId, ct);
            if (levelError != null)
                return ApiResponse<GuestResponse>.ErrorResponse(levelError);

            // Email identifies the PERSON, so it is immutable here: changing it
            // would silently re-identify the human behind every booking attached to
            // them, across every event. Renaming a person is a Guests-level concern,
            // not something an event's guest form should do as a side effect.
            if (!string.IsNullOrWhiteSpace(request.Email)
                && !string.Equals(request.Email.ToLower().Trim(), guest.Guest.Email, StringComparison.OrdinalIgnoreCase))
                return ApiResponse<GuestResponse>.ErrorResponse(
                    "A guest's email cannot be changed - it identifies them across every event.");

            // Not gated on the guest model: RequiredGuestFieldsJson validates the
            // guest RECORD, which isn't event-specific (§5), and the wizard already
            // checks it client-side on both models.
            var ruleError = await ValidateServiceLevelAssignmentAsync(serviceLevel, request, guest, ct);
            if (ruleError != null)
                return ApiResponse<GuestResponse>.ConflictResponse(ruleError, "SERVICE_LEVEL_RULE");

            var nationalityId = await ResolveNationalityIdAsync(request.NationalityId, ct);
            var organization = await ResolveOrganizationAsync(request.OrganizationId, ct);

            // -- person-level: shared by every event this human attends ---------
            var person = guest.Guest;
            person.FirstName     = request.FirstName?.Trim() ?? person.FirstName;
            person.LastName      = request.LastName?.Trim()  ?? person.LastName;
            person.NationalityId = nationalityId;
            person.PhotoUrl      = request.PhotoUrl;

            // Keep the linked User's denormalized display name in sync — it's
            // what admin-inbox/notification queries read for a guest's name.
            if (person.User != null)
            {
                person.User.FirstName = person.FirstName;
                person.User.LastName = person.LastName;
                _unitOfWork.Users.Update(person.User);
            }
            _unitOfWork.Guests.Update(person);

            // -- this event only ------------------------------------------------
            guest.GuestType     = request.GuestType ?? guest.GuestType;
            guest.Organization  = organization?.Name ?? request.Organization;
            guest.OrganizationId = organization?.Id;
            guest.ServiceLevelId = serviceLevel?.Id;
            if (serviceLevel != null && request.OverrideServiceLevelRules
                && _currentUser.HasPermission(PermissionCodes.ServiceLevelsOverrideRules))
            {
                guest.ServiceLevelRulesOverridden = true;
                guest.ServiceLevelOverrideReason = request.ServiceLevelOverrideReason?.Trim();
            }
            guest.AccreditationRequired = request.AccreditationRequired;
            // Same null-means-leave-alone rule as SessionIds below — a caller that
            // doesn't know about this field can't silently revoke the permissions.
            if (request.AllowedServices != null)
                guest.AllowedServicesJson = GuestServices.Serialize(request.AllowedServices);

            _unitOfWork.EventGuests.Update(guest);

            // Replace sessions only when the client explicitly sent the field (null = leave as-is)
            if (request.SessionIds != null)
            {
                if (guest.GuestSessions.Any())
                    _unitOfWork.GuestSessions.RemoveRange(guest.GuestSessions.ToList());

                foreach (var sessionId in await ResolveSessionIdsAsync(request.SessionIds, ct))
                    await _unitOfWork.GuestSessions.AddAsync(new GuestSession { EventGuestId = guest.Id, SessionId = sessionId }, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);

            if (request.InvitationTemplateId.HasValue && !string.IsNullOrWhiteSpace(person.Email))
                await SendInvitationAsync(guest, request.InvitationTemplateId.Value, ct);

            var updated = await LoadEventGuestGraph().FirstOrDefaultAsync(g => g.Id == guest.Id, ct);

            var response = _mapper.Map<GuestResponse>(updated);
            await MergeInvitationAsync(response, guest.Id, ct);
            return ApiResponse<GuestResponse>.SuccessResponse(response, "Guest updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating guest {GuestId}", request.Id);
            return ApiResponse<GuestResponse>.ServerErrorResponse("An error occurred while updating the guest");
        }
    }

    public async Task<ApiResponse<GuestResponse>> CreateGuestAsync(CreateGuestRequest request, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.FirstName) ||
                string.IsNullOrWhiteSpace(request.LastName) ||
                request.EventId == Guid.Empty)
                return ApiResponse<GuestResponse>.ErrorResponse("FirstName, LastName, and EventId are required");

            var ev = await _unitOfWork.Events.GetByPublicIdAsync(request.EventId, ct);
            if (ev == null)
                return ApiResponse<GuestResponse>.ErrorResponse("Event not found");

            // Assigned on both guest models — see the Update path for why.
            var usesLevels = EventGuestModels.UsesServiceLevels(ev.GuestModel);

            var (serviceLevel, levelError) =
                await ResolveServiceLevelAsync(request.ServiceLevelId, ev.Id, ct);
            if (levelError != null)
                return ApiResponse<GuestResponse>.ErrorResponse(levelError);

            // Email IS the person now, so it can't be optional.
            if (string.IsNullOrWhiteSpace(request.Email))
                return ApiResponse<GuestResponse>.ErrorResponse("Email is required");

            var normalised = request.Email.ToLower().Trim();

            // Find-or-refuse. One person per email, system-wide: a repeat guest must
            // reuse their Guests row and gain a second EventGuest, never a second
            // person record.
            var person = await _unitOfWork.Guests.Query()
                .FirstOrDefaultAsync(g => g.Email == normalised, ct);

            if (person != null)
            {
                var alreadyHere = await _unitOfWork.EventGuests.Query()
                    .AnyAsync(eg => eg.GuestId == person.Id && eg.EventId == ev.Id, ct);
                if (alreadyHere)
                    return ApiResponse<GuestResponse>.ConflictResponse(
                        $"{person.FirstName} {person.LastName} is already a guest of this event.",
                        "GUEST_ALREADY_IN_EVENT");

                // Existing person, different event. The New Guest wizard is told to
                // go through Add Existing Guest instead of silently creating a
                // duplicate human; Add Existing Guest sets LinkExistingPerson and
                // falls through to reuse this row.
                if (!request.LinkExistingPerson)
                {
                    var others = await _unitOfWork.EventGuests.Query()
                        .Where(eg => eg.GuestId == person.Id)
                        .Select(eg => eg.Event.Title)
                        .ToListAsync(ct);
                    var where = others.Count > 0 ? $" (in {string.Join(", ", others)})" : "";
                    return ApiResponse<GuestResponse>.ConflictResponse(
                        $"{person.FirstName} {person.LastName} already exists{where}. "
                        + "Use \"Existing Guest\" to add them to this event.",
                        "GUEST_EXISTS_ELSEWHERE");
                }
            }

            var ruleError = await ValidateServiceLevelAssignmentAsync(serviceLevel, request, null, ct);
            if (ruleError != null)
                return ApiResponse<GuestResponse>.ConflictResponse(ruleError, "SERVICE_LEVEL_RULE");

            var overrodeRules = serviceLevel != null && request.OverrideServiceLevelRules
                                && _currentUser.HasPermission(PermissionCodes.ServiceLevelsOverrideRules);

            var organization = await ResolveOrganizationAsync(request.OrganizationId, ct);

            // Only a genuinely new human gets a Guests row (and the User behind it).
            if (person == null)
            {
                var user = await CreateLinkedUserAsync(request.FirstName.Trim(), request.LastName.Trim(), ct);
                person = new Guest
                {
                    FirstName     = request.FirstName.Trim(),
                    LastName      = request.LastName.Trim(),
                    Email         = normalised,
                    NationalityId = await ResolveNationalityIdAsync(request.NationalityId, ct),
                    PhotoUrl      = request.PhotoUrl,
                    UserId        = user.Id,
                    CreatedAt     = DateTime.UtcNow,
                    IsDeleted     = false
                };
                await _unitOfWork.Guests.AddAsync(person, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            // The participation — everything that is true of this person only at
            // this event.
            var guest = new EventGuest
            {
                GuestId        = person.Id,
                EventId        = ev.Id,
                GuestType      = request.GuestType ?? GuestTypes.Delegate,
                Organization   = organization?.Name ?? request.Organization,
                OrganizationId = organization?.Id,
                ServiceLevelId = serviceLevel?.Id,
                ServiceLevelRulesOverridden = overrodeRules,
                ServiceLevelOverrideReason = overrodeRules ? request.ServiceLevelOverrideReason?.Trim() : null,
                AccreditationRequired = request.AccreditationRequired,
                AllowedServicesJson = GuestServices.Serialize(request.AllowedServices),
                CreatedAt      = DateTime.UtcNow,
                IsDeleted      = false
            };

            await _unitOfWork.EventGuests.AddAsync(guest, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            if (request.SessionIds is { Count: > 0 })
            {
                foreach (var sessionId in await ResolveSessionIdsAsync(request.SessionIds, ct))
                    await _unitOfWork.GuestSessions.AddAsync(new GuestSession { EventGuestId = guest.Id, SessionId = sessionId }, ct);

                await _unitOfWork.SaveChangesAsync(ct);
            }

            if (request.InvitationTemplateId.HasValue && !string.IsNullOrWhiteSpace(person.Email))
            {
                await SendInvitationAsync(guest, request.InvitationTemplateId.Value, ct);
            }
            else if (!request.InvitationTemplateId.HasValue)
            {
                // No template chosen at creation — no invite is going out, so there's
                // nothing for the guest to accept. Mark them accepted right away
                // instead of leaving them stuck at "not sent" (which would also block
                // accreditation — see AccreditationView's invitationStatus check).
                await _unitOfWork.Invitations.AddAsync(new Invitation
                {
                    EventGuestId = guest.Id,
                    InvitationStatus = GuestInvitationStatus.Accepted,
                    AccreditationStatus = GuestAccreditationStatus.NotIssued,
                }, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            var created = await LoadEventGuestGraph().FirstOrDefaultAsync(g => g.Id == guest.Id, ct);

            var response = _mapper.Map<GuestResponse>(created);
            await MergeInvitationAsync(response, guest.Id, ct);
            return ApiResponse<GuestResponse>.SuccessResponse(response, "Guest created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating guest");
            return ApiResponse<GuestResponse>.ServerErrorResponse("An error occurred while creating the guest");
        }
    }

    public async Task<ApiResponse<bool>> IssueAccreditationAsync(Guid guestId, CancellationToken ct = default)
        => await SetAccreditationStatusAsync(guestId, GuestAccreditationStatus.Issued, "Accreditation issued", ct);

    public async Task<ApiResponse<bool>> RevokeAccreditationAsync(Guid guestId, CancellationToken ct = default)
        => await SetAccreditationStatusAsync(guestId, GuestAccreditationStatus.NotIssued, "Accreditation revoked", ct);

    private async Task<ApiResponse<bool>> SetAccreditationStatusAsync(Guid guestId, string status, string successMessage, CancellationToken ct)
    {
        try
        {
            var guest = await _unitOfWork.EventGuests.GetByPublicIdAsync(guestId, ct);
            if (guest == null)
                return ApiResponse<bool>.NotFoundResponse("Guest not found");

            var invitation = await _unitOfWork.Invitations.Query()
                .FirstOrDefaultAsync(i => i.EventGuestId == guest.Id, ct);

            if (invitation == null)
            {
                invitation = new Invitation { EventGuestId = guest.Id, AccreditationStatus = status };
                await _unitOfWork.Invitations.AddAsync(invitation, ct);
            }
            else
            {
                invitation.AccreditationStatus = status;
                _unitOfWork.Invitations.Update(invitation);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            return ApiResponse<bool>.SuccessResponse(true, successMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting accreditation status for guest {GuestId}", guestId);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while updating accreditation");
        }
    }

    // Resolve a public nationality Guid to its internal int key.
    private async Task<int?> ResolveNationalityIdAsync(Guid? publicId, CancellationToken ct)
    {
        if (publicId == null || publicId == Guid.Empty) return null;
        var nat = await _unitOfWork.Nationalities.GetByPublicIdAsync(publicId.Value, ct);
        return nat?.Id;
    }

    // Resolve a public organization Guid to the entity itself — the caller
    // needs both its internal id (FK) and its Name (kept in sync on the
    // legacy free-text Organization column for every existing string-based
    // consumer — search, CSV export, travel rows, dashboard).
    private async Task<Organization> ResolveOrganizationAsync(Guid? publicId, CancellationToken ct)
    {
        if (publicId == null || publicId == Guid.Empty) return null;
        return await _unitOfWork.Organizations.GetByPublicIdAsync(publicId.Value, ct);
    }

    // ── Service level (replaces the old free-text Tier) ──────────────────────

    /// <summary>Resolves the public level id, rejecting a level that belongs to a
    /// different event — the level list is per-event, so a cross-event id is a
    /// client bug we should surface rather than silently accept.</summary>
    private async Task<(ServiceLevel level, string error)> ResolveServiceLevelAsync(
        Guid? publicId, int eventId, CancellationToken ct)
    {
        if (publicId == null || publicId == Guid.Empty) return (null, null);

        var level = await _unitOfWork.ServiceLevels.Query()
            .FirstOrDefaultAsync(l => l.PublicId == publicId.Value, ct);

        if (level == null) return (null, "Service level not found");
        if (!level.IsActive) return (null, $"\"{level.Name}\" is no longer available.");

        return (level, null);
    }

    /// <summary>
    /// Enforces the level's rules (capacity, required guest fields). Returns a
    /// message when the assignment should be blocked, or null to allow it.
    /// </summary>
    /// <remarks>
    /// Overridable by design: an authorised user (<see cref="PermissionCodes.ServiceLevelsOverrideRules"/>)
    /// can push through with <c>OverrideServiceLevelRules</c>. The permission is
    /// re-checked here rather than trusted from the request, so a client can't
    /// grant itself the bypass. Overrides are recorded on the guest row for audit.
    /// </remarks>
    // `existing` is the participation being edited, or null on create. Unused today
    // — capacity was dropped in v2 (see below), which was the only rule that needed
    // to know whether the guest was already on this level.
    private async Task<string> ValidateServiceLevelAssignmentAsync(
        ServiceLevel level, CreateGuestRequest request, EventGuest existing, CancellationToken ct)
    {
        if (level == null) return null;

        // Capacity was dropped in v2: a level is global, so a single "max guests"
        // number cannot mean anything across events. Required guest fields remain,
        // because they validate the guest record itself.
        var violations = new List<string>();

        var required = ServiceLevelRules.ParseRequiredFields(level.RequiredGuestFieldsJson);
        var missing = required.Where(key => !IsGuestFieldFilled(key, request)).ToList();
        if (missing.Count > 0)
            violations.Add($"\"{level.Name}\" requires: {string.Join(", ", missing.Select(GuestRequirableFields.Label))}.");

        if (violations.Count == 0) return null;

        var canOverride = _currentUser.HasPermission(PermissionCodes.ServiceLevelsOverrideRules);
        if (request.OverrideServiceLevelRules && canOverride)
        {
            _logger.LogInformation(
                "Service level rules overridden for level {Level} by user {UserId}. Violations: {Violations}. Reason: {Reason}",
                level.Name, _currentUser.UserId, string.Join(" ", violations),
                request.ServiceLevelOverrideReason ?? "(none given)");
            return null;
        }

        var suffix = canOverride
            ? " You can override this if it's intentional."
            : " Ask someone with override permission to place this guest.";
        return string.Join(" ", violations) + suffix;
    }

    private static bool IsGuestFieldFilled(string key, CreateGuestRequest r) => key switch
    {
        GuestRequirableFields.Email => !string.IsNullOrWhiteSpace(r.Email),
        GuestRequirableFields.NationalityId => r.NationalityId is { } n && n != Guid.Empty,
        GuestRequirableFields.OrganizationId => (r.OrganizationId is { } o && o != Guid.Empty)
                                                || !string.IsNullOrWhiteSpace(r.Organization),
        GuestRequirableFields.PhotoUrl => !string.IsNullOrWhiteSpace(r.PhotoUrl),
        // ArrivalDate/DepartureDate are gone from the guest record (they belong to
        // the Flight booking), so a level that still lists them can't block a save.
        _ => true,
    };

    // CSV import has no Guids to work with — match the Nationality column
    // against the lookup's Name or Code (case-insensitive) instead.
    private async Task<Guid?> ResolveNationalityByNameAsync(string nameOrCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nameOrCode)) return null;
        var term = nameOrCode.Trim().ToLower();
        var nat = await _unitOfWork.Nationalities.Query()
            .FirstOrDefaultAsync(n => n.Name.ToLower() == term || n.Code.ToLower() == term, ct);
        return nat?.PublicId;
    }

    // A real Excel date cell reads via GetDateTime(); a plain typed string
    // (or a value pasted from an older, unvalidated file) falls back to
    // DateOnly.TryParse. Empty is valid (no date given) — only unparseable
    // text fails.
    private static bool TryReadDateCell(IXLCell cell, out DateOnly? date)
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

    // 500 blank validated rows, same as the Events import template — plenty
    // for one batch without the file ballooning in size.
    private const int GuestImportTemplateRows = 500;

    public async Task<byte[]> BuildGuestImportTemplateAsync(Guid eventId, CancellationToken ct = default)
    {
        var eventEntity = await _unitOfWork.Events.Query().FirstOrDefaultAsync(e => e.PublicId == eventId, ct);

        var guestTypeNames = GuestEnumCatalog.All[GuestEnumCatalog.Type].Select(o => o.Name).ToList();
        var orgNames = await _unitOfWork.Organizations.Query().OrderBy(o => o.Name).Select(o => o.Name).ToListAsync(ct);
        var nationalityNames = await _unitOfWork.Nationalities.Query().OrderBy(n => n.Name).Select(n => n.Name).ToListAsync(ct);
        var levelNames = await _unitOfWork.ServiceLevels.Query().Where(l => l.IsActive)
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Name).Select(l => l.Name).ToListAsync(ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Guests");

        for (var i = 0; i < GuestImportHeaders.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = GuestImportHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#8d0134");
        }
        ws.SheetView.FreezeRows(1);
        for (var i = 1; i <= GuestImportHeaders.Length; i++) ws.Column(i).Width = 22;
        ws.Column(3).Width = 28; // Email

        // Hidden helper sheet for the dropdown columns' source lists — same
        // pattern as the Events import template. Each list gets its own
        // column on "Lists" (A, B, C, D — in the same order as `lists`).
        var listSheet = wb.Worksheets.Add("Lists");
        var lists = new[] { guestTypeNames, orgNames, nationalityNames, levelNames };
        for (var c = 0; c < lists.Length; c++)
            for (var i = 0; i < lists[c].Count; i++)
                listSheet.Cell(i + 1, c + 1).Value = lists[c][i];
        listSheet.Visibility = XLWorksheetVisibility.VeryHidden;

        void Dropdown(int col, int listCol, List<string> values, string title, string error)
        {
            if (values.Count == 0) return;
            var letter = (char)('A' + listCol);
            var range = $"Lists!${letter}$1:${letter}${values.Count}";
            var dv = ws.Range(2, col, GuestImportTemplateRows, col).SetDataValidation();
            dv.List(range, true);
            dv.IgnoreBlanks = true;
            dv.ErrorStyle = XLErrorStyle.Stop;
            dv.ErrorTitle = title;
            dv.ErrorMessage = error;
        }

        Dropdown(4, 0, guestTypeNames, "Invalid Guest Type", "Please pick a Guest Type from the dropdown — typing a value that isn't listed is not allowed.");
        Dropdown(5, 1, orgNames, "Invalid Organization", "Please pick an Organization from the dropdown, or leave this blank.");
        Dropdown(6, 2, nationalityNames, "Invalid Nationality", "Please pick a Nationality from the dropdown, or leave this blank.");
        Dropdown(7, 3, levelNames, "Invalid Service Level", "Please pick a Service Level from the dropdown, or leave this blank — you can assign one later from the Travel & Logistics page.");

        // Accreditation Required — a fixed TRUE/FALSE choice, not tied to a
        // live list, so it goes straight on the sheet rather than through Lists.
        var accredDv = ws.Range(2, 10, GuestImportTemplateRows, 10).SetDataValidation();
        accredDv.List("TRUE,FALSE", true);
        accredDv.IgnoreBlanks = true;
        accredDv.ErrorStyle = XLErrorStyle.Stop;
        accredDv.ErrorTitle = "Invalid value";
        accredDv.ErrorMessage = "Please pick TRUE or FALSE from the dropdown.";

        // Date validation (not just number formatting) is what makes Excel show
        // the calendar picker on these cells, bounded to this event's own dates
        // (with a week's slack either side — see GuestImportDateMarginDays).
        var minDate = eventEntity?.StartDate?.AddDays(-GuestImportDateMarginDays);
        var maxDate = eventEntity?.EndDate?.AddDays(GuestImportDateMarginDays);
        foreach (var col in new[] { 8, 9 })
        {
            ws.Column(col).Style.DateFormat.Format = "yyyy-mm-dd";
            if (minDate.HasValue && maxDate.HasValue)
            {
                var dvDate = ws.Range(2, col, GuestImportTemplateRows, col).SetDataValidation();
                dvDate.Date.Between(minDate.Value.ToDateTime(TimeOnly.MinValue), maxDate.Value.ToDateTime(TimeOnly.MinValue));
                dvDate.IgnoreBlanks = true;
                dvDate.ErrorStyle = XLErrorStyle.Stop;
                dvDate.ErrorTitle = "Invalid Date";
                dvDate.ErrorMessage = $"Date must be between {minDate:yyyy-MM-dd} and {maxDate:yyyy-MM-dd} — this event's dates, with a week's slack either side.";
            }
        }

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }

    private static bool ParseCsvBool(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.Trim().ToLowerInvariant();
        return v is "true" or "yes" or "y" or "1" or "required";
    }

    // Resolve public session Guids to internal int keys.
    private async Task<List<int>> ResolveSessionIdsAsync(IEnumerable<Guid> publicIds, CancellationToken ct)
    {
        var ids = publicIds.ToList();
        if (ids.Count == 0) return new List<int>();
        return await _unitOfWork.Sessions.Query()
            .Where(s => ids.Contains(s.PublicId))
            .Select(s => s.Id)
            .ToListAsync(ct);
    }
}
