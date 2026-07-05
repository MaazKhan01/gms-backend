using AutoMapper;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Guest;
using CsvHelper;
using CsvHelper.Configuration;
using DomainPersistence.Entities;
using Elasticsearch.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System.Globalization;

namespace Infrastructure.Services;

public class GuestService(
    IUnitOfWork _unitOfWork,
    IMapper _mapper,
    IEmailService _emailService,
    ILogger<GuestService> _logger) : IGuestService
{
    public async Task<ApiResponse<GuestResponse>> GetGuestByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var guest = await _unitOfWork.Guests.Query()
                .Include(g => g.GuestSessions)
                .Include(g => g.Nationality)
                .Include(g => g.InvitationTemplate)
                .FirstOrDefaultAsync(g => g.Id == id, ct);

            if (guest == null)
                return ApiResponse<GuestResponse>.NotFoundResponse("Guest not found");

            return ApiResponse<GuestResponse>.SuccessResponse(_mapper.Map<GuestResponse>(guest));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving guest {GuestId}", id);
            return ApiResponse<GuestResponse>.ServerErrorResponse("An error occurred while retrieving the guest");
        }
    }
    public async Task<ApiResponse<bool>> BulkGuestsDeleteAsync(Guid eventId, DeleteMultipleGuests request, CancellationToken ct = default)
    {
        try
        {
            var guests = await _unitOfWork.Guests
                .Query()
                .Where(g => request.SelectedGuestsToDelete.Contains(g.Id) && g.EventId == eventId)
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
    public async Task<ApiResponse<ImportGuestsResult>> ImportGuestCsvAsync(Guid eventId, Stream csvStream, Guid createdBy, CancellationToken ct = default)
    {
        var result = new ImportGuestsResult();
        try {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated  = null,
                MissingFieldFound = null,
            };
            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, config);

            var rows = csv.GetRecords<GuestRow>().ToList();
            foreach (var row in rows) {
                if (string.IsNullOrEmpty(row.FirstName) || string.IsNullOrEmpty(row.LastName))
                {
                    result.Skipped++;
                    result.Errors.Add($"Record skipped - Missing First or Last Name: '{row.Email}' ");
                    continue;
                }
                try {
                    var request = new CreateGuestRequest
                    {
                        FirstName = row.FirstName.Trim(),
                        LastName = row.LastName.Trim(),
                        Email = row.Email?.Trim() ?? null,
                        EventId = eventId,
                        GuestType  = string.IsNullOrEmpty(row.GuestType) ? "delegate" : row.GuestType.Trim().ToLower(),
                        Organization = row.Organization?.Trim() ?? null,
                        Tier = string.IsNullOrWhiteSpace(row.Tier) ? "Delegate" : row.Tier.Trim(),
                        InvitationStatus = string.IsNullOrWhiteSpace(row.InvitationStatus) ? "not_sent" : row.InvitationStatus.Trim(),
                        ArrivalDate = DateOnly.TryParse(row.ArrivalDate, out var d) ? d : null,
                        FlightNumber = row.FlightNumber?.Trim() ?? null,
                        Hotel = row.Hotel?.Trim() ?? null,
                        AccreditationStatus = string.IsNullOrWhiteSpace(row.AccreditationStatus) ? "not_issued" : row.AccreditationStatus.Trim(),

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
                catch (Exception ex) {
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
            var query = _unitOfWork.Guests.Query()
                .Include(g => g.Nationality)
                .Where(g => g.EventId == eventId);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLower();
                query = query.Where(g =>
                    g.FirstName.ToLower().Contains(term) ||
                    g.LastName.ToLower().Contains(term) ||
                    (g.Email != null && g.Email.ToLower().Contains(term)) ||
                    (g.Organization != null && g.Organization.ToLower().Contains(term)));
            }

            var total = await query.CountAsync(ct);

            var guests = await query
                .OrderBy(g => g.FirstName)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(ct);

            var paged = new PaginatedResponse<GuestResponse>(
                _mapper.Map<List<GuestResponse>>(guests), total, request.PageNumber, request.PageSize);

            return ApiResponse<PaginatedResponse<GuestResponse>>.SuccessResponse(paged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving guests for event {EventId}", eventId);
            return ApiResponse<PaginatedResponse<GuestResponse>>.ServerErrorResponse("An error occurred while retrieving guests");
        }
    }

    public async Task<ApiResponse<bool>> DeleteGuestByIdAsync(Guid id, CancellationToken ct = default) {
        try {
            var guest = await _unitOfWork.Guests.FindFirstOrDefaultAsync(g => g.Id == id);
            if (guest == null)
            {
                return ApiResponse<bool>.NotFoundResponse("Guest Not Found");
            }
            _unitOfWork.Guests.Remove(guest);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(_mapper.Map<bool>(true), "Guest Deleted Successfully!" );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting guest {GuestId}", id);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the guest");
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

            // Prevent duplicate email within the same event (only when email is provided)
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var normalised = request.Email.ToLower().Trim();
                var existing = await _unitOfWork.Guests.Query()
                    .FirstOrDefaultAsync(g => g.Email == normalised && g.EventId == request.EventId, ct);

                if (existing != null)
                    return ApiResponse<GuestResponse>.ConflictResponse("A guest with this email already exists for this event");
            }

            var guest = new Guest
            {
                Id                   = Guid.NewGuid(),
                FirstName            = request.FirstName.Trim(),
                LastName             = request.LastName.Trim(),
                Email                = request.Email?.ToLower().Trim(),
                EventId              = request.EventId,
                GuestType            = request.GuestType ?? GuestTypes.Delegate,
                Organization         = request.Organization,
                NationalityId        = request.NationalityId,
                Tier                 = request.Tier,
                InvitationStatus     = request.InvitationStatus ?? GuestInvitationStatus.NotSent,
                ArrivalDate          = request.ArrivalDate,
                FlightNumber         = request.FlightNumber,
                SeatId               = request.SeatId,
                Hotel                = request.Hotel,
                AccreditationStatus  = request.AccreditationStatus ?? GuestAccreditationStatus.NotIssued,
                InvitationTemplateId = request.InvitationTemplateId,
                CreatedAt            = DateTime.UtcNow,
                IsDeleted            = false
            };

            await _unitOfWork.Guests.AddAsync(guest, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Bind sessions after guest is saved so FK constraint is satisfied
            if (request.SessionIds is { Count: > 0 })
            {
                foreach (var sessionId in request.SessionIds)
                    await _unitOfWork.GuestSessions.AddAsync(new GuestSession { GuestId = guest.Id, SessionId = sessionId }, ct);

                await _unitOfWork.SaveChangesAsync(ct);
            }

            // Send invitation email if a template was selected
            if (request.InvitationTemplateId.HasValue && !string.IsNullOrWhiteSpace(guest.Email))
            {
                var template = await _unitOfWork.InvitationTemplates.Query()
                    .FirstOrDefaultAsync(t => t.Id == request.InvitationTemplateId.Value, ct);

                if (template != null)
                {
                    // Mark as sent before firing email so the status is correct in the response
                    guest.InvitationStatus = GuestInvitationStatus.Sent;
                    _unitOfWork.Guests.Update(guest);
                    await _unitOfWork.SaveChangesAsync(ct);

                    var guestName = $"{guest.FirstName} {guest.LastName}".Trim();
                    var emailSubject = template.Subject;
                    var emailBody = (template.Body ?? "")
                        .Replace("{{GuestName}}", guestName)
                        .Replace("{{FirstName}}", guest.FirstName)
                        .Replace("{{LastName}}", guest.LastName);

                    _ = Task.Run(async () =>
                    {
                        try { await _emailService.SendGuestInvitationAsync(guest.Email, guestName, emailSubject, emailBody); }
                        catch (Exception ex) { _logger.LogWarning(ex, "Could not send invitation email to {Email}", guest.Email); }
                    });
                }
            }

            // Reload with all includes for complete response
            var created = await _unitOfWork.Guests.Query()
                .Include(g => g.GuestSessions)
                .Include(g => g.Nationality)
                .Include(g => g.InvitationTemplate)
                .FirstOrDefaultAsync(g => g.Id == guest.Id, ct);

            return ApiResponse<GuestResponse>.SuccessResponse(_mapper.Map<GuestResponse>(created), "Guest created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating guest");
            return ApiResponse<GuestResponse>.ServerErrorResponse("An error occurred while creating the guest");
        }
    }
}

 sealed class GuestRow
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string GuestType { get; set; }
    public string Organization { get; set; }
    public string Tier { get; set; }
    public string InvitationStatus { get; set; }
    public string ArrivalDate { get; set; }  // "YYYY-MM-DD" string
    public string FlightNumber { get; set; }
    public string Hotel { get; set; }
    public string AccreditationStatus { get; set; }
}