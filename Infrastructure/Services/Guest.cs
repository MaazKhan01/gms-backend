using AutoMapper;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Guest;
using CsvHelper;
using CsvHelper.Configuration;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Infrastructure.Services;

// Core guest CRUD. Flight / accommodation / transport / invitation / accreditation
// are separate modules now (their own tables) — not handled here.
public class GuestService(
    IUnitOfWork _unitOfWork,
    IMapper _mapper,
    IConfiguration _configuration,
    ILogger<GuestService> _logger) : IGuestService
{
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
                        Tier = string.IsNullOrWhiteSpace(row.Tier) ? "Delegate" : row.Tier.Trim(),
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

            var updated = await _unitOfWork.Guests.Query()
                .Include(g => g.GuestSessions).ThenInclude(gs => gs.Session)
                .Include(g => g.Nationality)
                .Include(g => g.Event)
                .FirstOrDefaultAsync(g => g.Id == guest.Id, ct);

            return ApiResponse<GuestResponse>.SuccessResponse(_mapper.Map<GuestResponse>(updated), "Guest updated successfully");
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

            var created = await _unitOfWork.Guests.Query()
                .Include(g => g.GuestSessions).ThenInclude(gs => gs.Session)
                .Include(g => g.Nationality)
                .Include(g => g.Event)
                .FirstOrDefaultAsync(g => g.Id == guest.Id, ct);

            return ApiResponse<GuestResponse>.SuccessResponse(_mapper.Map<GuestResponse>(created), "Guest created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating guest");
            return ApiResponse<GuestResponse>.ServerErrorResponse("An error occurred while creating the guest");
        }
    }

    // Resolve a public nationality Guid to its internal int key.
    private async Task<int?> ResolveNationalityIdAsync(Guid? publicId, CancellationToken ct)
    {
        if (publicId == null || publicId == Guid.Empty) return null;
        var nat = await _unitOfWork.Nationalities.GetByPublicIdAsync(publicId.Value, ct);
        return nat?.Id;
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
    public string Tier { get; set; }
    public string ArrivalDate { get; set; }  // "YYYY-MM-DD" string
}
