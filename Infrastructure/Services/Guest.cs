using AutoMapper;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Guest;
using Core.ViewModel.Invitation;
using CsvHelper;
using CsvHelper.Configuration;
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
    IImportBatchService _importBatchService) : IGuestService
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
    private async Task SendInvitationAsync(Guest guest, Guid templateId, CancellationToken ct)
    {
        var template = await _unitOfWork.InvitationTemplates.Query()
            .FirstOrDefaultAsync(t => t.PublicId == templateId, ct);
        if (template == null) return;

        var invitation = await _unitOfWork.Invitations.Query()
            .FirstOrDefaultAsync(i => i.GuestId == guest.Id, ct);

        if (invitation == null)
        {
            invitation = new Invitation { GuestId = guest.Id, AccreditationStatus = GuestAccreditationStatus.NotIssued };
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
            Tier = guest.Tier,
            Reference = invitation.InvitationToken?.ToString("N")[..8].ToUpperInvariant(),
        };

        var email = guest.Email;
        _ = Task.Run(async () =>
        {
            try { await _emailService.SendGuestInvitationAsync(email, emailModel); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not send invitation email to {Email}", email); }
        });
    }
    public async Task<ApiResponse<GuestResponse>> GetGuestByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var guest = await _unitOfWork.Guests.Query()
                .Include(g => g.GuestSessions).ThenInclude(gs => gs.Session)
                .Include(g => g.Nationality)
                .Include(g => g.OrganizationRef)
                .Include(g => g.Event)
                .FirstOrDefaultAsync(g => g.PublicId == id, ct);

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
            .FirstOrDefaultAsync(i => i.GuestId == guestId, ct);

        response.InvitationStatus = invitation?.InvitationStatus ?? GuestInvitationStatus.NotSent;
        response.AccreditationStatus = invitation?.AccreditationStatus ?? GuestAccreditationStatus.NotIssued;
        response.InvitationTemplateId = invitation?.InvitationTemplate?.PublicId;
    }

    private async Task MergeInvitationsAsync(List<GuestResponse> responses, List<int> guestIds, CancellationToken ct)
    {
        if (guestIds.Count == 0) return;

        var invitations = await _unitOfWork.Invitations.Query()
            .Include(i => i.InvitationTemplate)
            .Where(i => guestIds.Contains(i.GuestId))
            .ToListAsync(ct);
        var byGuestId = invitations.ToDictionary(i => i.GuestId, i => i);

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

            var guests = await _unitOfWork.Guests
                .Query()
                .Where(g => request.SelectedGuestsToDelete.Contains(g.PublicId) && g.EventId == ev.Id)
                .ToListAsync(ct);

            if (!guests.Any())
                return ApiResponse<bool>.NotFoundResponse("No matching guests found");

            // Same Restrict-FK concern as DeleteGuestByIdAsync — free any seats
            // held by these guests before removing them.
            var guestIds = guests.Select(g => g.Id).ToList();
            var seatAssigns = await _unitOfWork.SeatAssigns.Query()
                .Where(sa => guestIds.Contains(sa.GuestId))
                .ToListAsync(ct);
            if (seatAssigns.Count > 0)
                _unitOfWork.SeatAssigns.RemoveRange(seatAssigns);

            _unitOfWork.Guests.RemoveRange(guests);
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

    // The Hangfire job body — same CSV parsing + per-row CreateGuestAsync calls
    // ImportGuestCsvAsync used to do synchronously, now writing outcomes to
    // ImportBatchRow and notifying on completion instead of returning them.
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
            using var csvStream = await _blobService.DownloadAsync(batch.FileUrl, ct);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null,
            };
            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, config);

            var rows = csv.GetRecords<GuestRow>().ToList();
            var rowNumber = 1; // header is row 1, first data row is 2
            foreach (var row in rows)
            {
                rowNumber++;
                var name = $"{row.FirstName} {row.LastName}".Trim();

                if (string.IsNullOrEmpty(row.FirstName) || string.IsNullOrEmpty(row.LastName))
                {
                    rowResults.Add(new ImportBatchRow
                    {
                        RowNumber = rowNumber, Title = name.Length > 0 ? name : row.Email, Success = false,
                        Error = "Missing First or Last Name.", ErrorCategory = "validation",
                    });
                    continue;
                }
                try
                {
                    var request = new CreateGuestRequest
                    {
                        FirstName = row.FirstName.Trim(),
                        LastName = row.LastName.Trim(),
                        Email = row.Email?.Trim() ?? null,
                        EventId = eventEntity.PublicId,
                        GuestType = string.IsNullOrEmpty(row.GuestType) ? "delegate" : row.GuestType.Trim().ToLower(),
                        Organization = row.Organization?.Trim() ?? null,
                        NationalityId = await ResolveNationalityByNameAsync(row.Nationality, ct),
                        Tier = string.IsNullOrWhiteSpace(row.Tier) ? "Delegate" : row.Tier.Trim(),
                        ArrivalDate = ParseCsvDate(row.ArrivalDate),
                        DepartureDate = ParseCsvDate(row.DepartureDate),
                        AccreditationRequired = ParseCsvBool(row.AccreditationRequired),
                    };
                    var createResult = await CreateGuestAsync(request, ct);
                    rowResults.Add(new ImportBatchRow
                    {
                        RowNumber = rowNumber, Title = name, Success = createResult.Success,
                        Error = createResult.Success ? null : createResult.Message,
                        ErrorCategory = createResult.Success ? null : "validation",
                    });
                }
                catch (Exception ex)
                {
                    rowResults.Add(new ImportBatchRow
                    { RowNumber = rowNumber, Title = name, Success = false, Error = ex.Message, ErrorCategory = "validation" });
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
            batch.ErrorMessage = "The uploaded CSV file contains invalid data or has an incorrect format.";
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

            var query = _unitOfWork.Guests.QueryNoTracking().Where(g => g.EventId == ev.Id);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.Trim();
                query = query.Where(g =>
                    g.FirstName.Contains(term) ||
                    g.LastName.Contains(term) ||
                    (g.Organization != null && g.Organization.Contains(term)));
            }

            query = query.Where(g => !_unitOfWork.Invitations.Query()
                .Any(i => i.GuestId == g.Id && i.InvitationStatus == GuestInvitationStatus.Declined));

            var total = await query.CountAsync(ct);

            var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;
            var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;

            var items = await query
                .OrderBy(g => g.FirstName).ThenBy(g => g.LastName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(g => new GuestPickerResponse
                {
                    Id = g.PublicId,
                    FullName = (g.FirstName + " " + g.LastName).Trim(),
                    Organization = g.Organization,
                    Tier = g.Tier,
                    PhotoUrl = g.PhotoUrl,
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

            var query = _unitOfWork.Guests.QueryNoTracking().Where(g => g.EventId != ev.Id);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.Trim();
                query = query.Where(g =>
                    g.FirstName.Contains(term) ||
                    g.LastName.Contains(term) ||
                    (g.Email != null && g.Email.Contains(term)) ||
                    (g.Organization != null && g.Organization.Contains(term)) ||
                    (g.OrganizationRef != null && g.OrganizationRef.Name.Contains(term)));
            }

            var total = await query.CountAsync(ct);

            var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;
            var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;

            var raw = await query
                .OrderBy(g => g.FirstName).ThenBy(g => g.LastName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(g => new
                {
                    GuestId = g.Id,
                    Row = new OtherEventGuestRow
                    {
                        Id = g.PublicId,
                        FirstName = g.FirstName,
                        LastName = g.LastName,
                        Email = g.Email,
                        GuestType = g.GuestType,
                        OrganizationId = g.OrganizationRef != null ? (Guid?)g.OrganizationRef.PublicId : null,
                        OrganizationName = g.OrganizationRef != null ? g.OrganizationRef.Name : g.Organization,
                        NationalityId = g.Nationality != null ? (Guid?)g.Nationality.PublicId : null,
                        NationalityName = g.Nationality != null ? g.Nationality.Name : null,
                        NationalityFlag = g.Nationality != null ? g.Nationality.Flag : null,
                        PhotoUrl = g.PhotoUrl,
                        Tier = g.Tier,
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
                    .Where(i => guestIds.Contains(i.GuestId))
                    .ToListAsync(ct);
                var byGuestId = invitations.ToDictionary(i => i.GuestId, i => i);

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

            var query = _unitOfWork.Guests.Query()
                .Include(g => g.Nationality)
                .Include(g => g.OrganizationRef)
                .Include(g => g.Event)
                .Include(g => g.GuestSessions).ThenInclude(gs => gs.Session)
                .Where(g => g.EventId == ev.Id);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLower();
                query = query.Where(g =>
                    g.FirstName.ToLower().Contains(term) ||
                    g.LastName.ToLower().Contains(term) ||
                    (g.Email != null && g.Email.ToLower().Contains(term)) ||
                    (g.Organization != null && g.Organization.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(request.Tier))
            {
                var tier = request.Tier.ToLower();
                query = query.Where(g => g.Tier.ToLower() == tier);
            }

            // Invitation status lives on the Invitation row, not the Guest — and
            // "not_sent" also covers guests who have no invitation row at all.
            if (!string.IsNullOrWhiteSpace(request.InvitationStatus))
            {
                var status = request.InvitationStatus;
                query = status == GuestInvitationStatus.NotSent
                    ? query.Where(g => !_unitOfWork.Invitations.Query()
                        .Any(i => i.GuestId == g.Id && i.InvitationStatus != GuestInvitationStatus.NotSent))
                    : query.Where(g => _unitOfWork.Invitations.Query()
                        .Any(i => i.GuestId == g.Id && i.InvitationStatus == status));
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
                            .Any(i => i.GuestId == g.Id && i.InvitationStatus != GuestInvitationStatus.NotSent))
                        || (otherStatuses.Count > 0 && _unitOfWork.Invitations.Query()
                            .Any(i => i.GuestId == g.Id && otherStatuses.Contains(i.InvitationStatus))));
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
                query = query.Where(g => nat != null && g.NationalityId == nat.Id);
            }

            // Accreditation: "not_required" (flag off) / "pending" (flag on, not
            // yet issued) / "issued" (flag on, Invitation.AccreditationStatus == Issued).
            if (!string.IsNullOrWhiteSpace(request.AccreditationStatus))
            {
                if (request.AccreditationStatus == "not_required")
                    query = query.Where(g => !g.AccreditationRequired);
                else if (request.AccreditationStatus == GuestAccreditationStatus.Issued)
                    query = query.Where(g => g.AccreditationRequired && _unitOfWork.Invitations.Query()
                        .Any(i => i.GuestId == g.Id && i.AccreditationStatus == GuestAccreditationStatus.Issued));
                else if (request.AccreditationStatus == "pending")
                    query = query.Where(g => g.AccreditationRequired && !_unitOfWork.Invitations.Query()
                        .Any(i => i.GuestId == g.Id && i.AccreditationStatus == GuestAccreditationStatus.Issued));
            }

            // Downstream pickers (seating/meetings/travel) pass excludeDeclined=true
            // so a guest who rejected their invitation can't be assigned anywhere.
            if (request.ExcludeDeclined)
            {
                query = query.Where(g => !_unitOfWork.Invitations.Query()
                    .Any(i => i.GuestId == g.Id && i.InvitationStatus == GuestInvitationStatus.Declined));
            }

            var total = await query.CountAsync(ct);

            var guests = await query
                .OrderBy(g => g.FirstName)
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
                .Where(sa => sa.GuestId == guest.Id)
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

            var guest = await _unitOfWork.Guests.Query()
                .Include(g => g.GuestSessions)
                .Include(g => g.Nationality)
                .Include(g => g.OrganizationRef)
                .Include(g => g.Event)
                .Include(g => g.User)
                .FirstOrDefaultAsync(g => g.PublicId == request.Id.Value, ct);

            if (guest == null)
                return ApiResponse<GuestResponse>.NotFoundResponse("Guest not found");

            // Duplicate email within the same event (excluding this guest)
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var normalised = request.Email.ToLower().Trim();
                var duplicate = await _unitOfWork.Guests.Query()
                    .FirstOrDefaultAsync(g => g.Email == normalised && g.EventId == guest.EventId && g.Id != guest.Id, ct);

                if (duplicate != null)
                    return ApiResponse<GuestResponse>.ConflictResponse("A guest with this email already exists for this event");
            }

            var nationalityId = await ResolveNationalityIdAsync(request.NationalityId, ct);
            var organization = await ResolveOrganizationAsync(request.OrganizationId, ct);

            guest.FirstName     = request.FirstName?.Trim() ?? guest.FirstName;
            guest.LastName      = request.LastName?.Trim()  ?? guest.LastName;
            guest.Email         = request.Email?.ToLower().Trim();

            // Keep the linked User's denormalized display name in sync — it's
            // what admin-inbox/notification queries read for a guest's name.
            if (guest.User != null)
            {
                guest.User.FirstName = guest.FirstName;
                guest.User.LastName = guest.LastName;
                _unitOfWork.Users.Update(guest.User);
            }
            guest.GuestType     = request.GuestType ?? guest.GuestType;
            guest.Organization  = organization?.Name ?? request.Organization;
            guest.OrganizationId = organization?.Id;
            guest.NationalityId = nationalityId;
            guest.Tier          = request.Tier ?? guest.Tier;
            guest.ArrivalDate   = request.ArrivalDate;
            guest.DepartureDate = request.DepartureDate;
            guest.PhotoUrl      = request.PhotoUrl;
            guest.AccreditationRequired = request.AccreditationRequired;
            // Same null-means-leave-alone rule as SessionIds below — a caller that
            // doesn't know about this field can't silently revoke the permissions.
            if (request.AllowedServices != null)
                guest.AllowedServicesJson = GuestServices.Serialize(request.AllowedServices);

            _unitOfWork.Guests.Update(guest);

            // Replace sessions only when the client explicitly sent the field (null = leave as-is)
            if (request.SessionIds != null)
            {
                if (guest.GuestSessions.Any())
                    _unitOfWork.GuestSessions.RemoveRange(guest.GuestSessions.ToList());

                foreach (var sessionId in await ResolveSessionIdsAsync(request.SessionIds, ct))
                    await _unitOfWork.GuestSessions.AddAsync(new GuestSession { GuestId = guest.Id, SessionId = sessionId }, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);

            if (request.InvitationTemplateId.HasValue && !string.IsNullOrWhiteSpace(guest.Email))
                await SendInvitationAsync(guest, request.InvitationTemplateId.Value, ct);

            var updated = await _unitOfWork.Guests.Query()
                .Include(g => g.GuestSessions).ThenInclude(gs => gs.Session)
                .Include(g => g.Nationality)
                .Include(g => g.OrganizationRef)
                .Include(g => g.Event)
                .FirstOrDefaultAsync(g => g.Id == guest.Id, ct);

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

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var normalised = request.Email.ToLower().Trim();
                var existing = await _unitOfWork.Guests.Query()
                    .FirstOrDefaultAsync(g => g.Email == normalised && g.EventId == ev.Id, ct);

                if (existing != null)
                    return ApiResponse<GuestResponse>.ConflictResponse("A guest with this email already exists for this event");
            }

            var organization = await ResolveOrganizationAsync(request.OrganizationId, ct);
            var user = await CreateLinkedUserAsync(request.FirstName.Trim(), request.LastName.Trim(), ct);
            var guest = new Guest
            {
                FirstName     = request.FirstName.Trim(),
                LastName      = request.LastName.Trim(),
                Email         = request.Email?.ToLower().Trim(),
                EventId       = ev.Id,
                GuestType     = request.GuestType ?? GuestTypes.Delegate,
                Organization  = organization?.Name ?? request.Organization,
                OrganizationId = organization?.Id,
                NationalityId = await ResolveNationalityIdAsync(request.NationalityId, ct),
                Tier          = request.Tier,
                ArrivalDate   = request.ArrivalDate,
                DepartureDate = request.DepartureDate,
                PhotoUrl      = request.PhotoUrl,
                AccreditationRequired = request.AccreditationRequired,
                AllowedServicesJson = GuestServices.Serialize(request.AllowedServices),
                UserId        = user.Id,
                CreatedAt     = DateTime.UtcNow,
                IsDeleted     = false
            };

            await _unitOfWork.Guests.AddAsync(guest, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            if (request.SessionIds is { Count: > 0 })
            {
                foreach (var sessionId in await ResolveSessionIdsAsync(request.SessionIds, ct))
                    await _unitOfWork.GuestSessions.AddAsync(new GuestSession { GuestId = guest.Id, SessionId = sessionId }, ct);

                await _unitOfWork.SaveChangesAsync(ct);
            }

            if (request.InvitationTemplateId.HasValue && !string.IsNullOrWhiteSpace(guest.Email))
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
                    GuestId = guest.Id,
                    InvitationStatus = GuestInvitationStatus.Accepted,
                    AccreditationStatus = GuestAccreditationStatus.NotIssued,
                }, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            var created = await _unitOfWork.Guests.Query()
                .Include(g => g.GuestSessions).ThenInclude(gs => gs.Session)
                .Include(g => g.Nationality)
                .Include(g => g.OrganizationRef)
                .Include(g => g.Event)
                .FirstOrDefaultAsync(g => g.Id == guest.Id, ct);

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
            var guest = await _unitOfWork.Guests.GetByPublicIdAsync(guestId, ct);
            if (guest == null)
                return ApiResponse<bool>.NotFoundResponse("Guest not found");

            var invitation = await _unitOfWork.Invitations.Query()
                .FirstOrDefaultAsync(i => i.GuestId == guest.Id, ct);

            if (invitation == null)
            {
                invitation = new Invitation { GuestId = guest.Id, AccreditationStatus = status };
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

    private static DateOnly? ParseCsvDate(string value)
        => DateOnly.TryParse(value, out var d) ? d : null;

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

sealed class GuestRow
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string GuestType { get; set; }
    public string Organization { get; set; }
    public string Nationality { get; set; }  // matched by Name or Code
    public string Tier { get; set; }
    public string ArrivalDate { get; set; }  // "YYYY-MM-DD" string
    public string DepartureDate { get; set; }  // "YYYY-MM-DD" string
    public string AccreditationRequired { get; set; }  // "true"/"yes"/"1"/"required"
}
