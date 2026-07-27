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
    ILogger<GuestService> _logger) : IGuestService
{
    private string FrontendUrl => _configuration.GetValue<string>("FrontendUrl") ?? "http://localhost:5173";

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

    public async Task<ApiResponse<ImportGuestsResult>> ImportGuestCsvAsync(Guid eventId, Stream csvStream, int createdBy, CancellationToken ct)
    {
        var result = new ImportGuestsResult();
        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null,
            };
            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, config);

            var rows = csv.GetRecords<GuestRow>().ToList();
            foreach (var row in rows)
            {
                if (string.IsNullOrEmpty(row.FirstName) || string.IsNullOrEmpty(row.LastName))
                {
                    result.Skipped++;
                    result.Errors.Add($"Record skipped - Missing First or Last Name: '{row.Email}' ");
                    continue;
                }
                try
                {
                    var request = new CreateGuestRequest
                    {
                        FirstName = row.FirstName.Trim(),
                        LastName = row.LastName.Trim(),
                        Email = row.Email?.Trim() ?? null,
                        EventId = eventId,
                        GuestType = string.IsNullOrEmpty(row.GuestType) ? "delegate" : row.GuestType.Trim().ToLower(),
                        Organization = row.Organization?.Trim() ?? null,
                        NationalityId = await ResolveNationalityByNameAsync(row.Nationality, ct),
                        Tier = string.IsNullOrWhiteSpace(row.Tier) ? "Delegate" : row.Tier.Trim(),
                        ArrivalDate = ParseCsvDate(row.ArrivalDate),
                        DepartureDate = ParseCsvDate(row.DepartureDate),
                        AccreditationRequired = ParseCsvBool(row.AccreditationRequired),
                    };
                    var createResult = await CreateGuestAsync(request, ct);
                    if (createResult.Success)
                        result.Imported++;
                    else
                    {
                        result.Skipped++;
                        result.Errors.Add($"Row failed ({row.FirstName} {row.LastName}): {createResult.Message}");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    result.Skipped++;
                    result.Errors.Add($"Row failed ({row.FirstName} {row.LastName}): {ex.Message}");
                }
            }
            return ApiResponse<ImportGuestsResult>.SuccessResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSV import failed.");
            return ApiResponse<ImportGuestsResult>.ErrorResponse("The uploaded CSV file contains invalid data or has an incorrect format.");
        }
    }

    public async Task<ApiResponse<PaginatedResponse<GuestResponse>>> GetGuestsAsync(Guid eventId, PagedRequest request, CancellationToken ct = default)
    {
        try
        {
            var ev = await _unitOfWork.Events.GetByPublicIdAsync(eventId, ct);
            if (ev == null)
                return ApiResponse<PaginatedResponse<GuestResponse>>.NotFoundResponse("Event not found");

            var query = _unitOfWork.Guests.Query()
                .Include(g => g.Nationality)
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
                .Include(g => g.Event)
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

            guest.FirstName     = request.FirstName?.Trim() ?? guest.FirstName;
            guest.LastName      = request.LastName?.Trim()  ?? guest.LastName;
            guest.Email         = request.Email?.ToLower().Trim();
            guest.GuestType     = request.GuestType ?? guest.GuestType;
            guest.Organization  = request.Organization;
            guest.NationalityId = nationalityId;
            guest.Tier          = request.Tier ?? guest.Tier;
            guest.ArrivalDate   = request.ArrivalDate;
            guest.DepartureDate = request.DepartureDate;
            guest.PhotoUrl      = request.PhotoUrl;
            guest.AccreditationRequired = request.AccreditationRequired;

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

            var guest = new Guest
            {
                FirstName     = request.FirstName.Trim(),
                LastName      = request.LastName.Trim(),
                Email         = request.Email?.ToLower().Trim(),
                EventId       = ev.Id,
                GuestType     = request.GuestType ?? GuestTypes.Delegate,
                Organization  = request.Organization,
                NationalityId = await ResolveNationalityIdAsync(request.NationalityId, ct),
                Tier          = request.Tier,
                ArrivalDate   = request.ArrivalDate,
                DepartureDate = request.DepartureDate,
                PhotoUrl      = request.PhotoUrl,
                AccreditationRequired = request.AccreditationRequired,
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
                await SendInvitationAsync(guest, request.InvitationTemplateId.Value, ct);

            var created = await _unitOfWork.Guests.Query()
                .Include(g => g.GuestSessions).ThenInclude(gs => gs.Session)
                .Include(g => g.Nationality)
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
