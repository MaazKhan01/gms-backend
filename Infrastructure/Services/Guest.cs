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
using Microsoft.Data.SqlClient;

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

    // Auto-provisions the User row every Guest is 1:1 with (see Guest.UserId
    // remarks) — RoleId -> the "guest" role (PortalAccess=false, no permissions),
    // PasswordHash left null (OTP via CurrentGuest/VipAppService remains the only
    // way in). Email AND UserName are both set to the guest's email: Guests.Email
    // is unique among active rows now, so the two filtered-unique indexes on Users
    // agree with it rather than fighting it, and every lookup that goes through
    // Users (support chat, notifications, driver chat) can find a guest by email.
    private async Task<User> BuildLinkedUserAsync(string email, string firstName, string lastName, CancellationToken ct)
    {
        var guestRole = await _unitOfWork.Roles.FindFirstOrDefaultAsync(r => r.Code == Roles.GUEST, ct);
        return new User
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            RoleId = guestRole?.Id,
        };
    }

    // A unique-index violation is how a create race lands here: two requests both
    // read "no such guest / not in this event yet", then one of the inserts loses.
    // 2601/2627 are SQL Server's duplicate-key numbers; anything else is a real
    // failure and must not be reported to the caller as a conflict.
    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is SqlException sql && sql.Number is 2601 or 2627;

    // Normalised form of a guest email — the one place the trim/lowercase rule
    // lives, so the uniqueness check and the stored value can never disagree.
    private static string NormalizeEmail(string email) => email?.Trim().ToLowerInvariant();

    // The person behind an email, or null. Deliberately not filtered by event:
    // Guest IS the cross-event identity now.
    private Task<Guest> FindPersonByEmailAsync(string normalisedEmail, CancellationToken ct)
        => _unitOfWork.Guests.Query()
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.Email == normalisedEmail, ct);

    // Every read of a participation that feeds GuestResponse — one place so the
    // list, the detail and the create/update echoes can't drift apart.
    private IQueryable<EventGuest> EventGuestGraph()
        => _unitOfWork.EventGuests.Query()
            .Include(eg => eg.Guest).ThenInclude(g => g.Nationality)
            .Include(eg => eg.Event)
            .Include(eg => eg.OrganizationRef)
            .Include(eg => eg.ServiceLevel)
            .Include(eg => eg.GuestSessions).ThenInclude(gs => gs.Session);

    // Upserts the participation's Invitation row (one per EventGuest) with a
    // fresh token and fires the branded email. Called from Create/UpdateGuestAsync
    // when an InvitationTemplateId is supplied — including to resend.
    private async Task SendInvitationAsync(EventGuest participation, Guid templateId, CancellationToken ct)
    {
        var template = await _unitOfWork.InvitationTemplates.Query()
            .FirstOrDefaultAsync(t => t.PublicId == templateId, ct);
        if (template == null) return;

        var guest = participation.Guest
            ?? await _unitOfWork.Guests.Query().FirstOrDefaultAsync(g => g.Id == participation.GuestId, ct);
        if (guest == null) return;

        var invitation = await _unitOfWork.Invitations.Query()
            .FirstOrDefaultAsync(i => i.EventGuestId == participation.Id, ct);

        if (invitation == null)
        {
            invitation = new Invitation { EventGuestId = participation.Id, AccreditationStatus = GuestAccreditationStatus.NotIssued };
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

        var ev = participation.Event
            ?? await _unitOfWork.Events.Query().FirstOrDefaultAsync(e => e.Id == participation.EventId, ct);
        var guestName = $"{guest.FirstName} {guest.LastName}".Trim();
        var link = $"{FrontendUrl}/?screen=invitation&token={invitation.InvitationToken}";

        // If the admin placed their own invite button in the body (via the
        // {{InviteLink}} placeholder), we swap in the real URL and suppress the
        // email shell's default CTA so there aren't two buttons. Otherwise the
        // shell appends its standard "View Invitation & Respond" button (as before).
        var rawBody = template.Body ?? "";
        var hasOwnButton = rawBody.Contains("{{InviteLink}}");
        var emailBody = rawBody
            .Replace("{{GuestName}}", guestName)
            .Replace("{{FirstName}}", guest.FirstName)
            .Replace("{{LastName}}", guest.LastName)
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
            Tier = participation.ServiceLevel?.Code,
            Reference = invitation.InvitationToken?.ToString("N")[..8].ToUpperInvariant(),
        };

        var email = guest.Email;
        _ = Task.Run(async () =>
        {
            try { await _emailService.SendGuestInvitationAsync(email, emailModel); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not send invitation email to {Email}", email); }
        });
    }
    /// <summary><paramref name="id"/> is an <c>EventGuest.PublicId</c> — one
    /// person's participation in one event, which is what the guest screens edit.</summary>
    public async Task<ApiResponse<GuestResponse>> GetGuestByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var participation = await EventGuestGraph().FirstOrDefaultAsync(eg => eg.PublicId == id, ct);

            if (participation == null)
                return ApiResponse<GuestResponse>.NotFoundResponse("Guest not found");

            var response = _mapper.Map<GuestResponse>(participation);
            await MergeInvitationAsync(response, participation.Id, ct);
            return ApiResponse<GuestResponse>.SuccessResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving guest {GuestId}", id);
            return ApiResponse<GuestResponse>.ServerErrorResponse("An error occurred while retrieving the guest");
        }
    }

    // Merges the participation's Invitation row (status/accreditation/template)
    // into an already-mapped GuestResponse — Invitation isn't part of the
    // EventGuest entity graph, so AutoMapper can't reach it on its own.
    private async Task MergeInvitationAsync(GuestResponse response, int eventGuestId, CancellationToken ct)
    {
        var invitation = await _unitOfWork.Invitations.Query()
            .Include(i => i.InvitationTemplate)
            .FirstOrDefaultAsync(i => i.EventGuestId == eventGuestId, ct);

        response.InvitationStatus = invitation?.InvitationStatus ?? GuestInvitationStatus.NotSent;
        response.AccreditationStatus = invitation?.AccreditationStatus ?? GuestAccreditationStatus.NotIssued;
        response.InvitationTemplateId = invitation?.InvitationTemplate?.PublicId;
    }

    private async Task MergeInvitationsAsync(List<GuestResponse> responses, List<int> eventGuestIds, CancellationToken ct)
    {
        if (eventGuestIds.Count == 0) return;

        var invitations = await _unitOfWork.Invitations.Query()
            .Include(i => i.InvitationTemplate)
            .Where(i => eventGuestIds.Contains(i.EventGuestId))
            .ToListAsync(ct);
        var byGuestId = invitations.ToDictionary(i => i.EventGuestId, i => i);

        for (var idx = 0; idx < responses.Count && idx < eventGuestIds.Count; idx++)
        {
            byGuestId.TryGetValue(eventGuestIds[idx], out var invitation);
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

            // The ids are EventGuest.PublicIds: removing a guest from an event
            // drops their participation, never the person (who may be on other events).
            var participations = await _unitOfWork.EventGuests
                .Query()
                .Where(eg => request.SelectedGuestsToDelete.Contains(eg.PublicId) && eg.EventId == ev.Id)
                .ToListAsync(ct);

            if (!participations.Any())
                return ApiResponse<bool>.NotFoundResponse("No matching guests found");

            await SoftDeleteParticipationsAsync(participations, ct);
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
    //
    // Duplicate emails therefore follow the same rules as the UI: a row whose
    // email is already on THIS event fails with "already on this event", while
    // one whose email exists on another event silently reuses that master Guest
    // and its login and only adds the participation. Nothing here dedupes people.
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
                        Email = email,
                        EventId = eventEntity.PublicId,
                        GuestType = guestTypeCode,
                        OrganizationId = organizationId,
                        NationalityId = await ResolveNationalityByNameAsync(nationalityName, ct),
                        ServiceLevelId = serviceLevelId,
                        ArrivalDate = arrivalDate,
                        DepartureDate = departureDate,
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

            var query = _unitOfWork.EventGuests.QueryNoTracking().Where(eg => eg.EventId == ev.Id);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.Trim();
                query = query.Where(eg =>
                    eg.Guest.FirstName.Contains(term) ||
                    eg.Guest.LastName.Contains(term) ||
                    (eg.Organization != null && eg.Organization.Contains(term)));
            }

            query = query.Where(eg => !_unitOfWork.Invitations.Query()
                .Any(i => i.EventGuestId == eg.Id && i.InvitationStatus == GuestInvitationStatus.Declined));

            var total = await query.CountAsync(ct);

            var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;
            var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;

            var items = await query
                .OrderBy(eg => eg.Guest.FirstName).ThenBy(eg => eg.Guest.LastName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(eg => new GuestPickerResponse
                {
                    Id = eg.PublicId,
                    PersonId = eg.Guest.PublicId,
                    FullName = (eg.Guest.FirstName + " " + eg.Guest.LastName).Trim(),
                    Email = eg.Guest.Email,
                    Organization = eg.Organization,
                    Tier = eg.ServiceLevel != null ? eg.ServiceLevel.Code : null,
                    PhotoUrl = eg.Guest.PhotoUrl,
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

            // Participations in OTHER events whose person is not already on this
            // one: adding someone who is already here is a conflict, not a pick.
            var query = _unitOfWork.EventGuests.QueryNoTracking()
                .Where(eg => eg.EventId != ev.Id)
                .Where(eg => !_unitOfWork.EventGuests.Query().Any(x => x.GuestId == eg.GuestId && x.EventId == ev.Id));

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.Trim();
                query = query.Where(eg =>
                    eg.Guest.FirstName.Contains(term) ||
                    eg.Guest.LastName.Contains(term) ||
                    (eg.Guest.Email != null && eg.Guest.Email.Contains(term)) ||
                    (eg.Organization != null && eg.Organization.Contains(term)) ||
                    (eg.OrganizationRef != null && eg.OrganizationRef.Name.Contains(term)));
            }

            var total = await query.CountAsync(ct);

            var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;
            var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;

            var raw = await query
                .OrderBy(eg => eg.Guest.FirstName).ThenBy(eg => eg.Guest.LastName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(eg => new
                {
                    EventGuestId = eg.Id,
                    Row = new OtherEventGuestRow
                    {
                        Id = eg.PublicId,
                        PersonId = eg.Guest.PublicId,
                        FirstName = eg.Guest.FirstName,
                        LastName = eg.Guest.LastName,
                        Email = eg.Guest.Email,
                        GuestType = eg.GuestType,
                        OrganizationId = eg.OrganizationRef != null ? (Guid?)eg.OrganizationRef.PublicId : null,
                        OrganizationName = eg.OrganizationRef != null ? eg.OrganizationRef.Name : eg.Organization,
                        NationalityId = eg.Guest.Nationality != null ? (Guid?)eg.Guest.Nationality.PublicId : null,
                        NationalityName = eg.Guest.Nationality != null ? eg.Guest.Nationality.Name : null,
                        NationalityFlag = eg.Guest.Nationality != null ? eg.Guest.Nationality.Flag : null,
                        PhotoUrl = eg.Guest.PhotoUrl,
                        Tier = eg.ServiceLevel != null ? eg.ServiceLevel.Code : null,
                        ServiceLevelId = eg.ServiceLevel != null ? (Guid?)eg.ServiceLevel.PublicId : null,
                        ServiceLevelName = eg.ServiceLevel != null ? eg.ServiceLevel.Name : null,
                        ServiceLevelColor = eg.ServiceLevel != null ? eg.ServiceLevel.Color : null,
                        AccreditationRequired = eg.AccreditationRequired,
                        EventId = eg.Event.PublicId,
                        EventTitle = eg.Event.Title,
                    },
                })
                .ToListAsync(ct);

            var items = raw.Select(r => r.Row).ToList();
            var eventGuestIds = raw.Select(r => r.EventGuestId).ToList();

            if (eventGuestIds.Count > 0)
            {
                var invitations = await _unitOfWork.Invitations.Query()
                    .Where(i => eventGuestIds.Contains(i.EventGuestId))
                    .ToListAsync(ct);
                var byGuestId = invitations.ToDictionary(i => i.EventGuestId, i => i);

                for (var idx = 0; idx < items.Count; idx++)
                {
                    byGuestId.TryGetValue(eventGuestIds[idx], out var invitation);
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

    /// <summary>The event's guest table: one row per participation, so the same
    /// person on two events appears once under each.</summary>
    public async Task<ApiResponse<PaginatedResponse<GuestResponse>>> GetGuestsAsync(Guid eventId, GuestPagedRequest request, CancellationToken ct = default)
    {
        try
        {
            var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
            if (ev == null)
                return ApiResponse<PaginatedResponse<GuestResponse>>.NotFoundResponse("Event not found");

            var query = EventGuestGraph().Where(eg => eg.EventId == ev.Id);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLower();
                query = query.Where(eg =>
                    eg.Guest.FirstName.ToLower().Contains(term) ||
                    eg.Guest.LastName.ToLower().Contains(term) ||
                    (eg.Guest.Email != null && eg.Guest.Email.ToLower().Contains(term)) ||
                    (eg.Organization != null && eg.Organization.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(request.Tier))
            {
                var tier = request.Tier.ToLower();
                query = query.Where(eg => eg.ServiceLevel != null && eg.ServiceLevel.Code.ToLower() == tier);
            }

            if (request.ServiceLevelId is { } levelPublicId && levelPublicId != Guid.Empty)
            {
                var level = await _unitOfWork.ServiceLevels.QueryNoTracking()
                    .FirstOrDefaultAsync(l => l.PublicId == levelPublicId, ct);
                // Unknown id must return nothing, not everything.
                query = level == null
                    ? query.Where(_ => false)
                    : query.Where(eg => eg.ServiceLevelId == level.Id);
            }

            // Invitation status lives on the Invitation row, not the participation
            // — and "not_sent" also covers guests who have no invitation row at all.
            if (!string.IsNullOrWhiteSpace(request.InvitationStatus))
            {
                var status = request.InvitationStatus;
                query = status == GuestInvitationStatus.NotSent
                    ? query.Where(eg => !_unitOfWork.Invitations.Query()
                        .Any(i => i.EventGuestId == eg.Id && i.InvitationStatus != GuestInvitationStatus.NotSent))
                    : query.Where(eg => _unitOfWork.Invitations.Query()
                        .Any(i => i.EventGuestId == eg.Id && i.InvitationStatus == status));
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
                    var otherStatuses = statuses.Where(x => x != GuestInvitationStatus.NotSent).ToList();
                    query = query.Where(eg =>
                        (includesNotSent && !_unitOfWork.Invitations.Query()
                            .Any(i => i.EventGuestId == eg.Id && i.InvitationStatus != GuestInvitationStatus.NotSent))
                        || (otherStatuses.Count > 0 && _unitOfWork.Invitations.Query()
                            .Any(i => i.EventGuestId == eg.Id && otherStatuses.Contains(i.InvitationStatus))));
                }
            }

            if (request.OrganizationId.HasValue && request.OrganizationId != Guid.Empty)
            {
                var org = await _unitOfWork.Organizations.GetByPublicIdAsync(request.OrganizationId.Value, ct);
                query = query.Where(eg => org != null && eg.OrganizationId == org.Id);
            }

            if (request.NationalityId.HasValue && request.NationalityId != Guid.Empty)
            {
                var nat = await _unitOfWork.Nationalities.GetByPublicIdAsync(request.NationalityId.Value, ct);
                query = query.Where(eg => nat != null && eg.Guest.NationalityId == nat.Id);
            }

            // Accreditation: "not_required" (flag off) / "pending" (flag on, not
            // yet issued) / "issued" (flag on, Invitation.AccreditationStatus == Issued).
            if (!string.IsNullOrWhiteSpace(request.AccreditationStatus))
            {
                if (request.AccreditationStatus == "not_required")
                    query = query.Where(eg => !eg.AccreditationRequired);
                else if (request.AccreditationStatus == GuestAccreditationStatus.Issued)
                    query = query.Where(eg => eg.AccreditationRequired && _unitOfWork.Invitations.Query()
                        .Any(i => i.EventGuestId == eg.Id && i.AccreditationStatus == GuestAccreditationStatus.Issued));
                else if (request.AccreditationStatus == "pending")
                    query = query.Where(eg => eg.AccreditationRequired && !_unitOfWork.Invitations.Query()
                        .Any(i => i.EventGuestId == eg.Id && i.AccreditationStatus == GuestAccreditationStatus.Issued));
            }

            // Downstream pickers (seating/meetings/travel) pass excludeDeclined=true
            // so a guest who rejected their invitation can't be assigned anywhere.
            if (request.ExcludeDeclined)
            {
                query = query.Where(eg => !_unitOfWork.Invitations.Query()
                    .Any(i => i.EventGuestId == eg.Id && i.InvitationStatus == GuestInvitationStatus.Declined));
            }

            var total = await query.CountAsync(ct);

            var participations = await query
                .OrderBy(eg => eg.Guest.FirstName)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(ct);

            var mapped = _mapper.Map<List<GuestResponse>>(participations);
            await MergeInvitationsAsync(mapped, participations.Select(eg => eg.Id).ToList(), ct);

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

    /// <summary>Removes participations from their event WITHOUT destroying history:
    /// the EventGuest row and its event-scoped records are soft-deleted, so past
    /// events stay auditable and the person, their login and their other events are
    /// untouched. Nothing here ever deletes a Guest or a User — one person attends
    /// many events, and their identity outlives any single one.</summary>
    private async Task SoftDeleteParticipationsAsync(List<EventGuest> participations, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var ids = participations.Select(eg => eg.Id).ToList();

        // The two link rows that carry no IsDeleted of their own are still removed
        // physically. SeatAssign additionally has to go: its unique
        // (SeatingId, SeatId) index is unfiltered, so a soft-deleted row would hold
        // the seat hostage forever.
        // ponytail: add [IsDeleted] = 0 to that index if seat history ever matters.
        var seatAssigns = await _unitOfWork.SeatAssigns.Query()
            .Where(sa => ids.Contains(sa.EventGuestId))
            .ToListAsync(ct);
        if (seatAssigns.Count > 0)
            _unitOfWork.SeatAssigns.RemoveRange(seatAssigns);

        var guestSessions = await _unitOfWork.GuestSessions.Query()
            .Where(gs => ids.Contains(gs.EventGuestId))
            .ToListAsync(ct);
        if (guestSessions.Count > 0)
            _unitOfWork.GuestSessions.RemoveRange(guestSessions);

        // Bookings and per-event state: flagged, not dropped. Child rows a level
        // further down (flight legs, transport status history) come back filtered
        // with their parent, so they need no separate pass.
        await SoftDeleteAsync(_unitOfWork.Accommodations, a => ids.Contains(a.EventGuestId), userId, ct);
        await SoftDeleteAsync(_unitOfWork.Transports, t => ids.Contains(t.EventGuestId), userId, ct);
        await SoftDeleteAsync(_unitOfWork.Flights, f => ids.Contains(f.EventGuestId), userId, ct);
        await SoftDeleteAsync(_unitOfWork.Invitations, i => ids.Contains(i.EventGuestId), userId, ct);
        await SoftDeleteAsync(_unitOfWork.GuestServiceEntries, e => ids.Contains(e.EventGuestId), userId, ct);

        foreach (var participation in participations)
        {
            participation.MarkAsDeleted(userId);
            _unitOfWork.EventGuests.Update(participation);
        }
    }

    private static async Task SoftDeleteAsync<T>(
        IGenericRepository<T> repo, System.Linq.Expressions.Expression<Func<T, bool>> match,
        int userId, CancellationToken ct) where T : AuditEntity
    {
        var rows = await repo.Query().Where(match).ToListAsync(ct);
        foreach (var row in rows)
        {
            row.MarkAsDeleted(userId);
            repo.Update(row);
        }
    }

    public async Task<ApiResponse<bool>> DeleteGuestByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            // Deletes the PARTICIPATION (id is an EventGuest.PublicId). The person
            // and their login survive — they may still be on other events, and
            // re-adding them later must not create a second identity.
            var participation = await _unitOfWork.EventGuests.FindFirstOrDefaultAsync(eg => eg.PublicId == id, ct);
            if (participation == null)
                return ApiResponse<bool>.NotFoundResponse("Guest Not Found");

            await SoftDeleteParticipationsAsync([participation], ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "Guest deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting guest {GuestId}", id);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the guest");
        }
    }

    /// <summary>
    /// Edits ONE participation. <c>request.Id</c> is the EventGuest.PublicId.
    /// </summary>
    /// <remarks>
    /// Person fields (name, photo, nationality) are written to the shared master
    /// Guest and so change everywhere that person appears; everything else is
    /// written only to the selected participation. Email is the person's identity
    /// and is rejected if it differs — changing it would silently re-point their
    /// login, refresh tokens, notifications and support thread at someone else.
    /// </remarks>
    public async Task<ApiResponse<GuestResponse>> UpdateGuestAsync(CreateGuestRequest request, CancellationToken ct)
    {
        try
        {
            if (request.Id == null || request.Id == Guid.Empty)
                return ApiResponse<GuestResponse>.ErrorResponse("Guest Id is required");

            var participation = await _unitOfWork.EventGuests.Query()
                .Include(eg => eg.GuestSessions)
                .Include(eg => eg.Guest).ThenInclude(g => g.User)
                .Include(eg => eg.Guest).ThenInclude(g => g.Nationality)
                .Include(eg => eg.OrganizationRef)
                .Include(eg => eg.ServiceLevel)
                .Include(eg => eg.Event)
                .FirstOrDefaultAsync(eg => eg.PublicId == request.Id.Value, ct);

            if (participation == null)
                return ApiResponse<GuestResponse>.NotFoundResponse("Guest not found");

            var guest = participation.Guest;

            // Immutable identity. Silence would be worse than a 400: the caller
            // would believe the address changed while every login/notification
            // still resolves against the old one.
            if (!string.IsNullOrWhiteSpace(request.Email)
                && !string.Equals(NormalizeEmail(request.Email), guest.Email, StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<GuestResponse>.ErrorResponse(
                    "A guest's email cannot be changed — it is their identity across every event, "
                    + "their login and their notifications. Remove them and add the correct person instead.");
            }

            // Service levels apply to BOTH guest models (docs/service-levels-v2.md
            // §4): the model decides how the level's services must be COMPLETED —
            // mandatory and in order on fixed, optional and in any order on flexible
            // — not whether a level is assigned at all.
            var (serviceLevel, levelError) =
                await ResolveServiceLevelAsync(request.ServiceLevelId, participation.EventId, ct);
            if (levelError != null)
                return ApiResponse<GuestResponse>.ErrorResponse(levelError);

            // Not gated on the guest model: RequiredGuestFieldsJson validates the
            // guest RECORD, which isn't event-specific (§5), and the wizard already
            // checks it client-side on both models.
            var ruleError = await ValidateServiceLevelAssignmentAsync(serviceLevel, request, ct);
            if (ruleError != null)
                return ApiResponse<GuestResponse>.ConflictResponse(ruleError, "SERVICE_LEVEL_RULE");

            var nationalityId = await ResolveNationalityIdAsync(request.NationalityId, ct);
            var organization = await ResolveOrganizationAsync(request.OrganizationId, ct);

            // ── Person-level: shared by every event this human is on ────────────
            guest.FirstName    = request.FirstName?.Trim() ?? guest.FirstName;
            guest.LastName     = request.LastName?.Trim()  ?? guest.LastName;
            guest.PhotoUrl     = request.PhotoUrl;
            guest.NationalityId = nationalityId;
            _unitOfWork.Guests.Update(guest);

            // Keep the linked User's denormalized display name in sync — it's
            // what admin-inbox/notification queries read for a guest's name.
            // Email/UserName stay put: they mirror the immutable Guest.Email.
            if (guest.User != null)
            {
                guest.User.FirstName = guest.FirstName;
                guest.User.LastName = guest.LastName;
                _unitOfWork.Users.Update(guest.User);
            }

            // ── Event-level: this participation only ────────────────────────────
            var detailsError = await ApplyNominationDetailsAsync(guest, participation, request, ct);
            if (detailsError != null) return ApiResponse<GuestResponse>.NotFoundResponse(detailsError);

            participation.GuestType      = request.GuestType ?? participation.GuestType;
            // Mission role: the one the DMS screens actually read. Resolved
            // right here rather than through the mapper so an unknown id is an
            // error the caller sees, not a silently dropped field.
            if (request.MissionRoleId.HasValue)
            {
                if (request.MissionRoleId.Value == Guid.Empty)
                {
                    participation.MissionRoleId = null;
                }
                else
                {
                    var role = await _unitOfWork.Roles.QueryNoTracking()
                        .FirstOrDefaultAsync(r => r.PublicId == request.MissionRoleId.Value, ct);
                    if (role == null)
                        return ApiResponse<GuestResponse>.NotFoundResponse("Mission role not found");
                    participation.MissionRoleId = role.Id;
                }
            }
            participation.Organization   = organization?.Name ?? request.Organization;
            participation.OrganizationId = organization?.Id;
            participation.ServiceLevelId = serviceLevel?.Id;
            // Keep the navigation in step with the key — the invitation email sent
            // further down reads the tier through it.
            participation.ServiceLevel   = serviceLevel;
            if (serviceLevel != null && request.OverrideServiceLevelRules
                && _currentUser.CanWrite(PermissionCodes.ServiceLevels))
            {
                participation.ServiceLevelRulesOverridden = true;
                participation.ServiceLevelOverrideReason = request.ServiceLevelOverrideReason?.Trim();
            }
            participation.AccreditationRequired = request.AccreditationRequired;
            // Same null-means-leave-alone rule as SessionIds below — a caller that
            // doesn't know about this field can't silently revoke the permissions.
            if (request.AllowedServices != null)
                participation.AllowedServicesJson = GuestServices.Serialize(request.AllowedServices);

            _unitOfWork.EventGuests.Update(participation);

            // Replace sessions only when the client explicitly sent the field (null = leave as-is)
            if (request.SessionIds != null)
            {
                if (participation.GuestSessions.Any())
                    _unitOfWork.GuestSessions.RemoveRange(participation.GuestSessions.ToList());

                foreach (var sessionId in await ResolveSessionIdsAsync(request.SessionIds, ct))
                    await _unitOfWork.GuestSessions.AddAsync(
                        new GuestSession { EventGuestId = participation.Id, SessionId = sessionId }, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);

            if (request.InvitationTemplateId.HasValue && !string.IsNullOrWhiteSpace(guest.Email))
                await SendInvitationAsync(participation, request.InvitationTemplateId.Value, ct);

            var updated = await EventGuestGraph().FirstOrDefaultAsync(eg => eg.Id == participation.Id, ct);

            var response = _mapper.Map<GuestResponse>(updated);
            await MergeInvitationAsync(response, participation.Id, ct);
            return ApiResponse<GuestResponse>.SuccessResponse(response, "Guest updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating guest {EventGuestId}", request.Id);
            return ApiResponse<GuestResponse>.ServerErrorResponse("An error occurred while updating the guest");
        }
    }

    /// <summary>
    /// Adds one person to one event. The email decides which person: a new one
    /// creates the master Guest and its linked login, an existing one is REUSED
    /// and simply gains a second participation — that is "add an existing guest
    /// to this event", not a new person.
    /// </summary>
    /// <remarks>
    /// Two requests can read "not in this event yet" at the same moment, so the
    /// database has the last word: the filtered unique indexes on Guests.Email
    /// and EventGuests(GuestId, EventId) turn the loser of that race into a
    /// duplicate-key error, which is translated to the same 409 the pre-check
    /// would have produced rather than surfacing as a 500.
    /// </remarks>
    public async Task<ApiResponse<GuestResponse>> CreateGuestAsync(CreateGuestRequest request, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.FirstName) ||
                string.IsNullOrWhiteSpace(request.LastName) ||
                request.EventId == Guid.Empty)
                return ApiResponse<GuestResponse>.ErrorResponse("FirstName, LastName, and EventId are required");

            var email = NormalizeEmail(request.Email);
            if (string.IsNullOrWhiteSpace(email))
                return ApiResponse<GuestResponse>.ErrorResponse("Email is required");

            var ev = await _unitOfWork.Events.GetByPublicIdAsync(request.EventId, ct);
            if (ev == null)
                return ApiResponse<GuestResponse>.ErrorResponse("Event not found");

            var (serviceLevel, levelError) =
                await ResolveServiceLevelAsync(request.ServiceLevelId, ev.Id, ct);
            if (levelError != null)
                return ApiResponse<GuestResponse>.ErrorResponse(levelError);

            var ruleError = await ValidateServiceLevelAssignmentAsync(serviceLevel, request, ct);
            if (ruleError != null)
                return ApiResponse<GuestResponse>.ConflictResponse(ruleError, "SERVICE_LEVEL_RULE");

            var (guest, personError) = await GetOrCreatePersonAsync(email, request, ct);
            if (personError != null)
                return ApiResponse<GuestResponse>.ConflictResponse(personError, "GUEST_EMAIL_CONFLICT");

            // Same email + same event is a conflict; same email + another event is
            // the reuse path and falls straight through to the insert below.
            var alreadyOnEvent = await _unitOfWork.EventGuests.Query()
                .AnyAsync(eg => eg.GuestId == guest.Id && eg.EventId == ev.Id, ct);
            if (alreadyOnEvent)
                return ApiResponse<GuestResponse>.ConflictResponse(
                    "This guest is already on this event.", "GUEST_ALREADY_ON_EVENT");

            var overrodeRules = serviceLevel != null && request.OverrideServiceLevelRules
                                && _currentUser.CanWrite(PermissionCodes.ServiceLevels);

            var organization = await ResolveOrganizationAsync(request.OrganizationId, ct);

            int? missionRoleId = null;
            if (request.MissionRoleId.HasValue && request.MissionRoleId.Value != Guid.Empty)
            {
                var role = await _unitOfWork.Roles.QueryNoTracking()
                    .FirstOrDefaultAsync(r => r.PublicId == request.MissionRoleId.Value, ct);
                if (role == null)
                    return ApiResponse<GuestResponse>.NotFoundResponse("Mission role not found");
                missionRoleId = role.Id;
            }

            var participation = new EventGuest
            {
                GuestId       = guest.Id,
                EventId       = ev.Id,
                GuestType     = request.GuestType ?? GuestTypes.Delegate,
                MissionRoleId = missionRoleId,
                Organization  = organization?.Name ?? request.Organization,
                OrganizationId = organization?.Id,
                ServiceLevelId = serviceLevel?.Id,
                // Set the navigation too, not just the key: the invitation email
                // sent further down reads the tier through it, and nothing reloads
                // this entity in between.
                ServiceLevel  = serviceLevel,
                ServiceLevelRulesOverridden = overrodeRules,
                ServiceLevelOverrideReason = overrodeRules ? request.ServiceLevelOverrideReason?.Trim() : null,
                AccreditationRequired = request.AccreditationRequired,
                AllowedServicesJson = GuestServices.Serialize(request.AllowedServices),
                CreatedAt     = DateTime.UtcNow,
                IsDeleted     = false,
            };

            var detailsError = await ApplyNominationDetailsAsync(guest, participation, request, ct);
            if (detailsError != null) return ApiResponse<GuestResponse>.NotFoundResponse(detailsError);

            await _unitOfWork.EventGuests.AddAsync(participation, ct);
            try
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Lost the race against a concurrent add of the same person.
                _logger.LogInformation(ex, "Concurrent add of guest {Email} to event {EventId}", email, ev.PublicId);
                return ApiResponse<GuestResponse>.ConflictResponse(
                    "This guest is already on this event.", "GUEST_ALREADY_ON_EVENT");
            }

            if (request.SessionIds is { Count: > 0 })
            {
                foreach (var sessionId in await ResolveSessionIdsAsync(request.SessionIds, ct))
                    await _unitOfWork.GuestSessions.AddAsync(
                        new GuestSession { EventGuestId = participation.Id, SessionId = sessionId }, ct);

                await _unitOfWork.SaveChangesAsync(ct);
            }

            if (request.InvitationTemplateId.HasValue)
            {
                await SendInvitationAsync(participation, request.InvitationTemplateId.Value, ct);
            }
            else
            {
                // No template chosen at creation — no invite is going out, so there's
                // nothing for the guest to accept. Mark them accepted right away
                // instead of leaving them stuck at "not sent" (which would also block
                // accreditation — see AccreditationView's invitationStatus check).
                await _unitOfWork.Invitations.AddAsync(new Invitation
                {
                    EventGuestId = participation.Id,
                    InvitationStatus = GuestInvitationStatus.Accepted,
                    AccreditationStatus = GuestAccreditationStatus.NotIssued,
                }, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            var created = await EventGuestGraph().FirstOrDefaultAsync(eg => eg.Id == participation.Id, ct);

            var response = _mapper.Map<GuestResponse>(created);
            await MergeInvitationAsync(response, participation.Id, ct);
            return ApiResponse<GuestResponse>.SuccessResponse(response, "Guest created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating guest");
            return ApiResponse<GuestResponse>.ServerErrorResponse("An error occurred while creating the guest");
        }
    }

    /// <summary>
    /// The master person for an email: the existing one if there is one, a brand
    /// new Guest + linked guest-role User otherwise. Returns (null, message) when
    /// the address is already taken by a non-guest portal account.
    /// </summary>
    /// <remarks>
    /// An existing person is returned as-is. Name/photo/nationality on the request
    /// are treated as this participation's copy of what the admin typed, not as an
    /// edit of the master record — overwriting a known person from an import row
    /// would let one careless spreadsheet rename them on every other event. Editing
    /// the person is what PUT /guest does.
    /// </remarks>
    private async Task<(Guest guest, string error)> GetOrCreatePersonAsync(
        string email, CreateGuestRequest request, CancellationToken ct)
    {
        var existing = await FindPersonByEmailAsync(email, ct);
        if (existing != null) return (existing, null);

        // A staff/driver account on the same address would collide with the
        // filtered unique indexes on Users.Email/UserName, so say so plainly
        // rather than letting SaveChanges throw.
        var takenByOtherUser = await _unitOfWork.Users.Query()
            .AnyAsync(u => u.Email == email && u.GuestProfile == null, ct);
        if (takenByOtherUser)
            return (null, $"\"{email}\" already belongs to a portal user account.");

        var guest = new Guest
        {
            FirstName     = request.FirstName.Trim(),
            LastName      = request.LastName.Trim(),
            Email         = email,
            NationalityId = await ResolveNationalityIdAsync(request.NationalityId, ct),
            PhotoUrl      = request.PhotoUrl,
            // Set through the navigation, not UserId: EF then inserts the User and
            // the Guest in ONE SaveChanges. Saving the User first would leave an
            // orphaned account holding the email if the Guest insert then lost a
            // race, and that account would block every later attempt.
            User          = await BuildLinkedUserAsync(email, request.FirstName.Trim(), request.LastName.Trim(), ct),
            CreatedAt     = DateTime.UtcNow,
            IsDeleted     = false,
        };

        await _unitOfWork.Guests.AddAsync(guest, ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Another request created the same person between our read and this
            // insert. Theirs won; nothing of ours was committed, so drop our copy
            // and carry on with the winner.
            _logger.LogInformation(ex, "Concurrent creation of guest person {Email}", email);
            // Both are still in the Added state, so Remove detaches rather than
            // deletes. The User has to go too — left tracked it would be inserted
            // by the next SaveChanges as an account belonging to nobody, holding
            // the email against every future attempt.
            _unitOfWork.Guests.Remove(guest);
            if (guest.User != null) _unitOfWork.Users.Remove(guest.User);
            var winner = await FindPersonByEmailAsync(email, ct);
            return winner != null
                ? (winner, null)
                : (null, "Could not create this guest — please try again.");
        }

        return (guest, null);
    }

    /// <summary>Accreditation is per event, so <paramref name="eventGuestId"/> is
    /// an EventGuest.PublicId.</summary>
    public async Task<ApiResponse<bool>> IssueAccreditationAsync(Guid eventGuestId, CancellationToken ct = default)
        => await SetAccreditationStatusAsync(eventGuestId, GuestAccreditationStatus.Issued, "Accreditation issued", ct);

    public async Task<ApiResponse<bool>> RevokeAccreditationAsync(Guid eventGuestId, CancellationToken ct = default)
        => await SetAccreditationStatusAsync(eventGuestId, GuestAccreditationStatus.NotIssued, "Accreditation revoked", ct);

    private async Task<ApiResponse<bool>> SetAccreditationStatusAsync(Guid eventGuestId, string status, string successMessage, CancellationToken ct)
    {
        try
        {
            var participation = await _unitOfWork.EventGuests.GetByPublicIdAsync(eventGuestId, ct);
            if (participation == null)
                return ApiResponse<bool>.NotFoundResponse("Guest not found");

            var invitation = await _unitOfWork.Invitations.Query()
                .FirstOrDefaultAsync(i => i.EventGuestId == participation.Id, ct);

            if (invitation == null)
            {
                invitation = new Invitation { EventGuestId = participation.Id, AccreditationStatus = status };
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
            _logger.LogError(ex, "Error setting accreditation status for participation {EventGuestId}", eventGuestId);
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

    /// <summary>
    /// The nomination details the Add Delegate form collects: what HR later
    /// verifies, captured while the delegate is being entered.
    ///
    /// Split deliberately — the first five belong to the PERSON and follow them
    /// from mission to mission, visa and insurance belong to THIS participation
    /// because a visa is for one trip. Null leaves a value alone, so a form that
    /// does not send a field cannot blank it.
    /// </summary>
    private async Task<string> ApplyNominationDetailsAsync(
        DomainPersistence.Entities.Guest person, EventGuest participation,
        CreateGuestRequest request, CancellationToken ct)
    {
        if (request.DepartmentId.HasValue)
        {
            if (request.DepartmentId.Value == Guid.Empty)
            {
                person.DepartmentId = null;
            }
            else
            {
                var dept = await _unitOfWork.Departments.GetByPublicIdAsync(request.DepartmentId.Value, ct);
                if (dept == null) return "Department not found";
                person.DepartmentId = dept.Id;
            }
        }

        if (request.JobTitle != null) person.JobTitle = request.JobTitle.Trim();
        if (request.EmploymentGrade != null) person.EmploymentGrade = request.EmploymentGrade.Trim();
        if (request.PassportNumber != null) person.PassportNumber = request.PassportNumber.Trim();
        if (request.PassportExpiry.HasValue) person.PassportExpiry = request.PassportExpiry;

        if (participation != null)
        {
            if (!string.IsNullOrWhiteSpace(request.VisaStatus)) participation.VisaStatus = request.VisaStatus;
            if (!string.IsNullOrWhiteSpace(request.InsuranceStatus)) participation.InsuranceStatus = request.InsuranceStatus;
        }

        return null;
    }

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
    /// Overridable by design: an authorised user (write access on Service Levels)
    /// can push through with <c>OverrideServiceLevelRules</c>. The permission is
    /// re-checked here rather than trusted from the request, so a client can't
    /// grant itself the bypass. Overrides are recorded on the guest row for audit.
    /// </remarks>
    private async Task<string> ValidateServiceLevelAssignmentAsync(
        ServiceLevel level, CreateGuestRequest request, CancellationToken ct)
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

        var canOverride = _currentUser.CanWrite(PermissionCodes.ServiceLevels);
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
        // Unknown keys read as "filled" — a retired requirable field (arrivalDate /
        // departureDate) left in a Service Level's JSON must not block a save.
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
