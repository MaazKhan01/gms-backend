using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Nomination;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

/// <summary>
/// Phase 2 — assembling a mission's delegation, and HR's sign-off on it.
///
/// A nomination IS an EventGuest row: the same participation every other
/// mission-scoped record hangs off. Nominating fills the mission-role fields on
/// it; the services, accreditation and seating layers fill the rest later.
/// </summary>
public class NominationService(
    IUnitOfWork _unitOfWork,
    ILogger<NominationService> _logger) : INominationService
{
    /// <summary>A passport is flagged when it expires within this window of the
    /// mission starting — the workflow's six-month rule.</summary>
    private const int PassportWarningMonths = 6;

    // ── Roster ───────────────────────────────────────────────────────────

    public async Task<ApiResponse<List<NominationResponse>>> GetRosterAsync(Guid eventId, CancellationToken ct = default)
    {
        var mission = await FindMissionAsync(eventId, ct);
        if (mission == null)
            return ApiResponse<List<NominationResponse>>.NotFoundResponse("Mission not found.");

        var roster = await LoadRosterAsync(mission.Id, ct);
        await ApplyFlagsAsync(roster, mission, ct);

        return ApiResponse<List<NominationResponse>>.SuccessResponse(roster);
    }

    /// <summary>
    /// The staff directory as it stands against one mission.
    ///
    /// <paramref name="includeOnRoster"/> false is the picker's view — people
    /// who could still be added. True is the Nominations SCREEN's view: the
    /// whole directory, each row saying whether they are already on, so the one
    /// table answers both "who is on this mission" and "who could be".
    /// </summary>
    public async Task<ApiResponse<PaginatedResponse<NominationCandidateResponse>>> GetCandidatesAsync(
        Guid eventId, Guid? departmentId, PagedRequest request, bool includeOnRoster, CancellationToken ct = default)
    {
        var page = request?.PageNumber > 0 ? request.PageNumber : 1;
        var size = request?.PageSize > 0 ? request.PageSize : 20;

        var mission = await FindMissionAsync(eventId, ct);
        if (mission == null)
            return ApiResponse<PaginatedResponse<NominationCandidateResponse>>.NotFoundResponse("Mission not found.");

        int? departmentInternalId = null;
        if (departmentId.HasValue && departmentId.Value != Guid.Empty)
        {
            var dept = await _unitOfWork.Departments.GetByPublicIdAsync(departmentId.Value, ct);
            if (dept == null)
                return ApiResponse<PaginatedResponse<NominationCandidateResponse>>.NotFoundResponse("Department not found.");
            departmentInternalId = dept.Id;
        }

        // The STAFF DIRECTORY, read against this mission. Two populations, and
        // the rules for them are different:
        //
        // 1. ANYONE ALREADY ON THIS MISSION. Listed unconditionally. This screen
        //    has to be a superset of the roster — a delegate added straight from
        //    the Delegates screen is on the mission whether or not they were
        //    ever "staff", and a roster member missing from the roster view is
        //    just a bug wearing a rule.
        //
        // 2. EVERYONE ELSE WHO COULD BE NOMINATED. Here a department is what
        //    makes someone ours: the workflow has Department Heads nominating
        //    their own people, and without it the pool was every Guest row in
        //    the database. Anyone committed to a DIFFERENT mission is also left
        //    out — they are that mission's delegate, and borrowing them has to
        //    be a decision rather than a click on a name that happened to be in
        //    the list. Delegates ▸ Add Delegate ▸ Existing Delegate is that
        //    decision; once made, clause 1 brings them back in as "On roster".
        var query = _unitOfWork.Guests.QueryNoTracking()
            .Where(g => g.EventGuests.Any(eg => eg.EventId == mission.Id)
                     || (g.DepartmentId != null
                         && !g.EventGuests.Any(eg => eg.EventId != mission.Id)));

        if (!includeOnRoster)
            query = query.Where(g => !g.EventGuests.Any(eg => eg.EventId == mission.Id));

        if (departmentInternalId.HasValue)
            query = query.Where(g => g.DepartmentId == departmentInternalId.Value);

        if (!string.IsNullOrWhiteSpace(request?.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(g =>
                g.FirstName.ToLower().Contains(term) ||
                g.LastName.ToLower().Contains(term) ||
                (g.Email != null && g.Email.ToLower().Contains(term)));
        }

        // Captured outside the expression tree — EF cannot translate DateTime.Now.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // The six-month passport rule, measured from the mission's start.
        var passportFloor = (mission.StartDate ?? today).AddMonths(PassportWarningMonths);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(g => g.FirstName).ThenBy(g => g.LastName)
            .Skip((page - 1) * size).Take(size)
            .Select(g => new NominationCandidateResponse
            {
                PersonId = g.PublicId,
                FullName = (g.FirstName + " " + g.LastName).Trim(),
                Email = g.Email,
                JobTitle = g.JobTitle,
                EmploymentGrade = g.EmploymentGrade,
                PhotoUrl = g.PhotoUrl,
                DepartmentId = g.Department != null ? (Guid?)g.Department.PublicId : null,
                DepartmentName = g.Department != null ? g.Department.Name : null,
                // Missions they are already committed to, so the picker can say
                // so rather than leaving a familiar name looking like a leak
                // from another mission's roster.
                CurrentMissions = g.EventGuests
                    .Where(eg => eg.EventId != mission.Id
                              && (eg.Event.EndDate == null || eg.Event.EndDate >= today))
                    .Select(eg => eg.Event.Title)
                    .ToList(),
                // Deliberately excludes THIS mission: without that, everyone
                // already on the roster overlapped themselves and the whole
                // warning read as noise.
                OverlapsThisMission = g.EventGuests.Any(eg =>
                    eg.EventId != mission.Id
                    && eg.Event.StartDate != null && eg.Event.EndDate != null
                    && mission.StartDate != null && mission.EndDate != null
                    && eg.Event.StartDate <= mission.EndDate && eg.Event.EndDate >= mission.StartDate),
                OverlappingMissions = g.EventGuests
                    .Where(eg => eg.EventId != mission.Id
                              && eg.Event.StartDate != null && eg.Event.EndDate != null
                              && mission.StartDate != null && mission.EndDate != null
                              && eg.Event.StartDate <= mission.EndDate
                              && eg.Event.EndDate >= mission.StartDate)
                    .Select(eg => eg.Event.Title)
                    .ToList(),

                // "On roster" means NOMINATED. A participation can exist without
                // one — the Delegates screen creates rows directly — and calling
                // that on-roster told the coordinator they had nominated somebody
                // they had not, and hid the button that would have.
                OnRoster = g.EventGuests.Any(eg => eg.EventId == mission.Id && eg.NominatedOn != null),
                // Separate flag: on the mission, but not put forward yet. The
                // row still needs its participation id so it can be edited or
                // removed without being nominated first.
                OnMissionNotNominated = g.EventGuests.Any(eg => eg.EventId == mission.Id && eg.NominatedOn == null),
                ParticipationId = g.EventGuests
                    .Where(eg => eg.EventId == mission.Id)
                    .Select(eg => (Guid?)eg.PublicId).FirstOrDefault(),
                MissionRoleId = g.EventGuests
                    .Where(eg => eg.EventId == mission.Id && eg.MissionRole != null)
                    .Select(eg => (Guid?)eg.MissionRole.PublicId).FirstOrDefault(),
                MissionRoleName = g.EventGuests
                    .Where(eg => eg.EventId == mission.Id)
                    .Select(eg => eg.MissionRole.Name).FirstOrDefault(),
                Subgroup = g.EventGuests
                    .Where(eg => eg.EventId == mission.Id)
                    .Select(eg => eg.Subgroup).FirstOrDefault(),
                VisaStatus = g.EventGuests
                    .Where(eg => eg.EventId == mission.Id)
                    .Select(eg => eg.VisaStatus).FirstOrDefault(),
                HrVerificationStatus = g.EventGuests
                    .Where(eg => eg.EventId == mission.Id)
                    .Select(eg => eg.HrVerificationStatus).FirstOrDefault(),
                HrVerificationNote = g.EventGuests
                    .Where(eg => eg.EventId == mission.Id)
                    .Select(eg => eg.HrVerificationNote).FirstOrDefault(),
                VisaRequired = g.EventGuests
                    .Where(eg => eg.EventId == mission.Id)
                    .Select(eg => eg.VisaRequired).FirstOrDefault(),
                InsuranceStatus = g.EventGuests
                    .Where(eg => eg.EventId == mission.Id)
                    .Select(eg => eg.InsuranceStatus).FirstOrDefault(),

                PassportNumber = g.PassportNumber,
                PassportExpiry = g.PassportExpiry,
                PassportExpiringSoon = g.PassportExpiry != null && g.PassportExpiry < passportFloor,
            })
            .ToListAsync(ct);

        return ApiResponse<PaginatedResponse<NominationCandidateResponse>>.SuccessResponse(
            new PaginatedResponse<NominationCandidateResponse>(items, total, page, size));
    }

    public async Task<ApiResponse<List<MissionRoleResponse>>> GetMissionRolesAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.Roles.QueryNoTracking()
            .Where(r => r.IsDelegateRole)
            .OrderBy(r => r.Name)
            .Select(r => new MissionRoleResponse
            {
                Id = r.PublicId,
                Code = r.Code,
                Name = r.Name,
                PortalAccess = r.PortalAccess,
            })
            .ToListAsync(ct);

        return ApiResponse<List<MissionRoleResponse>>.SuccessResponse(data);
    }

    // ── Nominate ─────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a person to the staff directory, and nominates them when a mission
    /// is named. This is the only way into the directory: creating a guest
    /// always attaches them to an event, so it cannot say "this person works
    /// here" without also putting them on a mission.
    /// </summary>
    public async Task<ApiResponse<NominationResponse>> CreateStaffAsync(
        CreateStaffRequest request, int userId, CancellationToken ct = default)
    {
        if (request == null) return ApiResponse<NominationResponse>.ErrorResponse("Request body is required.");
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return ApiResponse<NominationResponse>.ErrorResponse("First and last name are required.");

        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return ApiResponse<NominationResponse>.ErrorResponse("A valid email is required.");

        // A department is what makes someone staff — see GetCandidatesAsync.
        if (!request.DepartmentId.HasValue || request.DepartmentId.Value == Guid.Empty)
            return ApiResponse<NominationResponse>.ErrorResponse("Department is required.");

        var department = await _unitOfWork.Departments.GetByPublicIdAsync(request.DepartmentId.Value, ct);
        if (department == null)
            return ApiResponse<NominationResponse>.NotFoundResponse("Department not found.");

        int? nationalityId = null;
        if (request.NationalityId.HasValue && request.NationalityId.Value != Guid.Empty)
        {
            var nat = await _unitOfWork.Nationalities.GetByPublicIdAsync(request.NationalityId.Value, ct);
            if (nat == null) return ApiResponse<NominationResponse>.NotFoundResponse("Nationality not found.");
            nationalityId = nat.Id;
        }

        var person = await _unitOfWork.Guests.Query().FirstOrDefaultAsync(g => g.Email == email, ct);

        if (person == null)
        {
            // The address must not already belong to a portal account — the
            // filtered unique indexes on Users would reject it at SaveChanges,
            // and a constraint name is not an error message.
            var taken = await _unitOfWork.Users.QueryNoTracking()
                .AnyAsync(u => u.Email == email && u.GuestProfile == null, ct);
            if (taken)
                return ApiResponse<NominationResponse>.ConflictResponse(
                    $"\"{email}\" already belongs to a portal user account.", "EMAIL_TAKEN");

            var guestRole = await _unitOfWork.Roles.FindFirstOrDefaultAsync(r => r.Code == "guest", ct);

            person = new Guest
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = email,
                DepartmentId = department.Id,
                JobTitle = request.JobTitle?.Trim(),
                EmploymentGrade = request.EmploymentGrade?.Trim(),
                NationalityId = nationalityId,
                PassportNumber = request.PassportNumber?.Trim(),
                PassportExpiry = request.PassportExpiry,
                // Through the navigation, not UserId: EF then inserts the User
                // and the Guest in ONE SaveChanges, so a failed Guest insert
                // cannot leave an orphaned account holding the address.
                User = new User
                {
                    UserName = email,
                    Email = email,
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    IsActive = true,
                    RoleId = guestRole?.Id,
                },
            };
            person.SetCreationAudit(userId);
            await _unitOfWork.Guests.AddAsync(person, ct);
        }
        else
        {
            // Already known — this is "promote them into the directory", not a
            // rename. Only the staff fields are filled, and only where blank,
            // so re-adding someone never overwrites what is already recorded.
            person.DepartmentId = department.Id;
            person.JobTitle = string.IsNullOrWhiteSpace(person.JobTitle) ? request.JobTitle?.Trim() : person.JobTitle;
            person.EmploymentGrade = string.IsNullOrWhiteSpace(person.EmploymentGrade) ? request.EmploymentGrade?.Trim() : person.EmploymentGrade;
            person.PassportNumber = string.IsNullOrWhiteSpace(person.PassportNumber) ? request.PassportNumber?.Trim() : person.PassportNumber;
            person.PassportExpiry ??= request.PassportExpiry;
            person.NationalityId ??= nationalityId;
            person.SetUpdateAudit(userId);
            _unitOfWork.Guests.Update(person);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // No mission named: the person is in the directory, nothing more to do.
        if (!request.EventId.HasValue || request.EventId.Value == Guid.Empty)
            return ApiResponse<NominationResponse>.SuccessResponse(null, "Added to the staff directory.");

        var nominated = await NominateAsync(new CreateNominationRequest
        {
            EventId = request.EventId.Value,
            PersonId = person.PublicId,
            MissionRoleId = request.MissionRoleId,
            Subgroup = request.Subgroup,
        }, userId, ct);

        // The person is in the directory either way — that insert already
        // committed. Saying only "cap reached" would leave the caller thinking
        // nothing happened, and re-adding them would then look like a duplicate.
        if (!nominated.Success)
            nominated.Message = $"Added to the staff directory, but not nominated: {nominated.Message}";

        return nominated;
    }

    public async Task<ApiResponse<NominationResponse>> NominateAsync(
        CreateNominationRequest request, int userId, CancellationToken ct = default)
    {
        if (request == null) return ApiResponse<NominationResponse>.ErrorResponse("Request body is required.");
        if (request.EventId == Guid.Empty) return ApiResponse<NominationResponse>.ErrorResponse("Mission id is required.");
        if (request.PersonId == Guid.Empty) return ApiResponse<NominationResponse>.ErrorResponse("Person id is required.");

        var mission = await FindMissionAsync(request.EventId, ct);
        if (mission == null) return ApiResponse<NominationResponse>.NotFoundResponse("Mission not found.");

        // A finished mission takes no new delegates. Reports stay open — that is
        // the whole point of them — so this guard is here, not on the mission.
        if (IsCompleted(mission))
            return ApiResponse<NominationResponse>.ConflictResponse(
                "This mission has ended and can no longer take new delegates.", "MISSION_COMPLETED");

        var person = await _unitOfWork.Guests.QueryNoTracking()
            .FirstOrDefaultAsync(g => g.PublicId == request.PersonId, ct);
        if (person == null) return ApiResponse<NominationResponse>.NotFoundResponse("Person not found.");

        // A participation may already exist without being a nomination: the
        // Delegates screen creates one directly. Nominating that person is a
        // PROMOTION of the row they already have, not a second row — doing it
        // any other way would either fail as a duplicate or split their
        // bookings across two participations.
        var existing = await _unitOfWork.EventGuests.Query()
            .FirstOrDefaultAsync(eg => eg.EventId == mission.Id && eg.GuestId == person.Id, ct);

        if (existing is { NominatedOn: not null })
            return ApiResponse<NominationResponse>.ConflictResponse(
                "This person has already been nominated to the mission.", "ALREADY_ON_MISSION");

        var roleCheck = await ResolveMissionRoleAsync(
            request.MissionRoleId, mission.Id, existing?.Id, ct);
        if (roleCheck.Error != null) return roleCheck.Error.As<NominationResponse>();

        // Cap check last among the reads, so a clearer error wins when several
        // apply. It counts NOMINATIONS: the cap is what the host agreed to
        // receive, and someone merely added to the mission has not been put
        // forward to them yet. Promoting an existing row does not consume a new
        // slot, so it skips the check.
        if (mission.DelegationCap.HasValue && existing == null)
        {
            var onRoster = await _unitOfWork.EventGuests.QueryNoTracking()
                .CountAsync(eg => eg.EventId == mission.Id && eg.NominatedOn != null, ct);
            if (onRoster >= mission.DelegationCap.Value)
                return ApiResponse<NominationResponse>.ConflictResponse(
                    $"The host's cap of {mission.DelegationCap} delegate(s) is already reached.",
                    "DELEGATION_CAP_REACHED");
        }

        if (existing != null)
        {
            existing.MissionRoleId = roleCheck.RoleId ?? existing.MissionRoleId;
            if (!string.IsNullOrWhiteSpace(request.Subgroup)) existing.Subgroup = request.Subgroup.Trim();
            existing.NominatedOn = DateTime.UtcNow;
            existing.NominatedBy = userId == 0 ? null : userId;
            existing.HrVerificationStatus = HrVerificationStatuses.Pending;
            existing.SetUpdateAudit(userId);

            _unitOfWork.EventGuests.Update(existing);
            await _unitOfWork.SaveChangesAsync(ct);

            // The mission role may grant portal access — see DelegateAccountRole.
            await DelegateAccountRole.SyncAsync(_unitOfWork, existing.GuestId, ct);

            return await GetOneAsync(existing.PublicId, mission, ct);
        }

        var participation = new EventGuest
        {
            GuestId = person.Id,
            EventId = mission.Id,
            GuestType = GuestTypes.Delegate,
            MissionRoleId = roleCheck.RoleId,
            Subgroup = string.IsNullOrWhiteSpace(request.Subgroup) ? null : request.Subgroup.Trim(),
            NominatedOn = DateTime.UtcNow,
            NominatedBy = userId == 0 ? null : userId,
            HrVerificationStatus = HrVerificationStatuses.Pending,
        };
        participation.SetCreationAudit(userId);

        await _unitOfWork.EventGuests.AddAsync(participation, ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Lost a race against a concurrent nomination of the same person.
            _logger.LogInformation(ex, "Concurrent nomination of person {PersonId} to mission {EventId}",
                request.PersonId, request.EventId);
            return ApiResponse<NominationResponse>.ConflictResponse(
                "This person is already on the mission.", "ALREADY_ON_MISSION");
        }

        await DelegateAccountRole.SyncAsync(_unitOfWork, participation.GuestId, ct);

        return await GetOneAsync(participation.PublicId, mission, ct);
    }

    public async Task<ApiResponse<NominationResponse>> UpdateAsync(
        Guid id, UpdateNominationRequest request, int userId, CancellationToken ct = default)
    {
        if (id == Guid.Empty) return ApiResponse<NominationResponse>.ErrorResponse("Nomination id is required.");
        if (request == null) return ApiResponse<NominationResponse>.ErrorResponse("Request body is required.");

        var participation = await _unitOfWork.EventGuests.Query()
            .Include(eg => eg.Event)
            .FirstOrDefaultAsync(eg => eg.PublicId == id, ct);
        if (participation == null)
            return ApiResponse<NominationResponse>.NotFoundResponse("Nomination not found.");

        var roleCheck = await ResolveMissionRoleAsync(
            request.MissionRoleId, participation.EventId, participation.Id, ct);
        if (roleCheck.Error != null) return roleCheck.Error.As<NominationResponse>();

        if (request.MissionRoleId.HasValue) participation.MissionRoleId = roleCheck.RoleId;
        participation.Subgroup = string.IsNullOrWhiteSpace(request.Subgroup) ? null : request.Subgroup.Trim();
        participation.SetUpdateAudit(userId);

        _unitOfWork.EventGuests.Update(participation);
        await _unitOfWork.SaveChangesAsync(ct);

        await DelegateAccountRole.SyncAsync(_unitOfWork, participation.GuestId, ct);

        return await GetOneAsync(participation.PublicId, participation.Event, ct);
    }

    public async Task<ApiResponse<bool>> RemoveAsync(Guid id, int userId, CancellationToken ct = default)
    {
        if (id == Guid.Empty) return ApiResponse<bool>.ErrorResponse("Nomination id is required.");

        var participation = await _unitOfWork.EventGuests.Query()
            .FirstOrDefaultAsync(eg => eg.PublicId == id, ct);
        if (participation == null)
            return ApiResponse<bool>.NotFoundResponse("Nomination not found.");

        // Refuse once anything hangs off the participation. Removing would either
        // orphan those records or cascade them away silently; both are worse than
        // making the coordinator cancel the bookings first.
        var blockers = new List<string>();
        if (await _unitOfWork.Flights.QueryNoTracking().AnyAsync(f => f.EventGuestId == participation.Id, ct))
            blockers.Add("flights");
        if (await _unitOfWork.Accommodations.QueryNoTracking().AnyAsync(a => a.EventGuestId == participation.Id, ct))
            blockers.Add("accommodation");
        if (await _unitOfWork.Transports.QueryNoTracking().AnyAsync(t => t.EventGuestId == participation.Id, ct))
            blockers.Add("transport");
        if (await _unitOfWork.SeatAssigns.QueryNoTracking().AnyAsync(s => s.EventGuestId == participation.Id, ct))
            blockers.Add("a seat assignment");

        if (blockers.Count > 0)
            return ApiResponse<bool>.ConflictResponse(
                $"Cannot remove — this delegate still has {string.Join(", ", blockers)}. Cancel those first.",
                "NOMINATION_HAS_BOOKINGS");

        participation.MarkAsDeleted(userId);
        _unitOfWork.EventGuests.Update(participation);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.SuccessResponse(true, "Delegate removed from the mission.");
    }

    // ── HR verification ──────────────────────────────────────────────────

    public async Task<ApiResponse<List<NominationResponse>>> GetForVerificationAsync(
        Guid eventId, string status, CancellationToken ct = default)
    {
        var mission = await FindMissionAsync(eventId, ct);
        if (mission == null)
            return ApiResponse<List<NominationResponse>>.NotFoundResponse("Mission not found.");

        string filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            filter = status.Trim().ToLowerInvariant();
            if (!HrVerificationStatuses.IsValid(filter))
                return ApiResponse<List<NominationResponse>>.ErrorResponse(
                    $"Invalid status '{status}'. Expected pending, verified or rejected.");
        }

        // HR verifies people who have been NOMINATED (workflow step 7 follows
        // step 5). A participation created any other way — the Delegates screen
        // adds one directly — has no NominatedOn and has not been through this
        // phase, so it must not appear in HR's queue.
        var roster = (await LoadRosterAsync(mission.Id, filter, ct))
            .Where(r => r.NominatedOn != null)
            .ToList();
        await ApplyFlagsAsync(roster, mission, ct);

        return ApiResponse<List<NominationResponse>>.SuccessResponse(roster);
    }

    public Task<ApiResponse<HrVerificationResult>> VerifyAsync(
        HrVerifyRequest request, int userId, CancellationToken ct = default)
        => SetVerificationAsync(request?.Ids, HrVerificationStatuses.Verified, request?.Note, userId, ct);

    public Task<ApiResponse<HrVerificationResult>> RejectAsync(
        HrRejectRequest request, int userId, CancellationToken ct = default)
    {
        // A rejection with no reason gives the coordinator nothing to act on.
        if (string.IsNullOrWhiteSpace(request?.Note))
            return Task.FromResult(ApiResponse<HrVerificationResult>.ErrorResponse(
                "A reason is required when rejecting a nomination."));

        return SetVerificationAsync(request.Ids, HrVerificationStatuses.Rejected, request.Note, userId, ct);
    }

    public Task<ApiResponse<HrVerificationResult>> RevertVerificationAsync(
        HrRevertRequest request, int userId, CancellationToken ct = default)
        => SetVerificationAsync(request?.Ids, HrVerificationStatuses.Pending, request?.Note, userId, ct);

    /// <summary>
    /// Bulk verify/reject. Partial success by design: ids that are unknown or
    /// already in the target state are reported as skips rather than failing the
    /// whole batch, because HR works through a list and two people may overlap.
    /// </summary>
    private async Task<ApiResponse<HrVerificationResult>> SetVerificationAsync(
        List<Guid> ids, string targetStatus, string note, int userId, CancellationToken ct)
    {
        var wanted = (ids ?? new List<Guid>()).Where(i => i != Guid.Empty).Distinct().ToList();
        if (wanted.Count == 0)
            return ApiResponse<HrVerificationResult>.ErrorResponse("At least one nomination id is required.");

        var rows = await _unitOfWork.EventGuests.Query()
            .Where(eg => wanted.Contains(eg.PublicId))
            .ToListAsync(ct);

        var result = new HrVerificationResult();
        var found = rows.Select(r => r.PublicId).ToHashSet();

        foreach (var missing in wanted.Where(i => !found.Contains(i)))
            result.Skips.Add(new HrVerificationSkip { Id = missing, Reason = "Nomination not found." });

        foreach (var row in rows)
        {
            if (row.HrVerificationStatus == targetStatus)
            {
                result.Skips.Add(new HrVerificationSkip { Id = row.PublicId, Reason = $"Already {targetStatus}." });
                continue;
            }

            row.HrVerificationStatus = targetStatus;

            if (targetStatus == HrVerificationStatuses.Pending)
            {
                // Back in the queue, so there is no sign-off to point at. Keeping
                // the old verifier and date would read as though the row were
                // still decided — and the nomination letter, which names verified
                // delegates only, would disagree with the screen.
                row.HrVerifiedOn = null;
                row.HrVerifiedBy = null;
            }
            else
            {
                row.HrVerifiedOn = DateTime.UtcNow;
                row.HrVerifiedBy = userId == 0 ? null : userId;
            }

            // The note survives a revert: it is the only record of why the
            // decision was withdrawn.
            row.HrVerificationNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            row.SetUpdateAudit(userId);

            _unitOfWork.EventGuests.Update(row);
            result.Updated++;
        }

        result.Skipped = result.Skips.Count;
        if (result.Updated > 0) await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<HrVerificationResult>.SuccessResponse(result,
            $"{result.Updated} nomination(s) {targetStatus}, {result.Skipped} skipped.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private Task<Event> FindMissionAsync(Guid eventId, CancellationToken ct)
        => eventId == Guid.Empty
            ? Task.FromResult<Event>(null)
            : _unitOfWork.Events.QueryNoTracking().FirstOrDefaultAsync(e => e.PublicId == eventId, ct);

    private static bool IsCompleted(Event mission)
        => mission.EndDate.HasValue && mission.EndDate.Value < DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record RoleCheck(int? RoleId, ApiResponse<bool> Error);

    /// <summary>Validates the chosen mission role and enforces one Head of
    /// Delegation per mission. <paramref name="excludeParticipationId"/> lets an
    /// update keep its own role.</summary>
    private async Task<RoleCheck> ResolveMissionRoleAsync(
        Guid? missionRoleId, int missionId, int? excludeParticipationId, CancellationToken ct)
    {
        if (!missionRoleId.HasValue || missionRoleId.Value == Guid.Empty)
            return new RoleCheck(null, null);

        var role = await _unitOfWork.Roles.QueryNoTracking()
            .FirstOrDefaultAsync(r => r.PublicId == missionRoleId.Value, ct);

        if (role == null)
            return new RoleCheck(null, ApiResponse<bool>.NotFoundResponse("Mission role not found."));

        if (!role.IsDelegateRole)
            return new RoleCheck(null, ApiResponse<bool>.ErrorResponse(
                $"'{role.Name}' is not a delegate role. Tick \"delegate role\" on it first."));

        // One Head of Delegation per mission — the protocol order depends on there
        // being exactly one. Relax here if a mission ever needs co-heads.
        if (role.Code == DelegateRoleCodes.HeadOfDelegation)
        {
            var taken = await _unitOfWork.EventGuests.QueryNoTracking()
                .AnyAsync(eg => eg.EventId == missionId
                             && eg.MissionRoleId == role.Id
                             && (excludeParticipationId == null || eg.Id != excludeParticipationId), ct);
            if (taken)
                return new RoleCheck(null, ApiResponse<bool>.ConflictResponse(
                    "This mission already has a Head of Delegation.", "HEAD_OF_DELEGATION_TAKEN"));
        }

        return new RoleCheck(role.Id, null);
    }

    private async Task<ApiResponse<NominationResponse>> GetOneAsync(Guid publicId, Event mission, CancellationToken ct)
    {
        var rows = await LoadRosterAsync(mission.Id, null, ct, publicId);
        if (rows.Count == 0)
            return ApiResponse<NominationResponse>.NotFoundResponse("Nomination not found.");

        await ApplyFlagsAsync(rows, mission, ct);
        return ApiResponse<NominationResponse>.SuccessResponse(rows[0]);
    }

    private Task<List<NominationResponse>> LoadRosterAsync(int missionId, CancellationToken ct)
        => LoadRosterAsync(missionId, null, ct);

    private async Task<List<NominationResponse>> LoadRosterAsync(
        int missionId, string hrStatus, CancellationToken ct, Guid? onlyPublicId = null)
    {
        var query = _unitOfWork.EventGuests.QueryNoTracking().Where(eg => eg.EventId == missionId);

        if (onlyPublicId.HasValue) query = query.Where(eg => eg.PublicId == onlyPublicId.Value);
        if (hrStatus != null) query = query.Where(eg => eg.HrVerificationStatus == hrStatus);

        return await query
            .OrderBy(eg => eg.Subgroup).ThenBy(eg => eg.Guest.FirstName)
            .Select(eg => new NominationResponse
            {
                Id = eg.PublicId,
                PersonId = eg.Guest.PublicId,
                FullName = (eg.Guest.FirstName + " " + eg.Guest.LastName).Trim(),
                Email = eg.Guest.Email,
                JobTitle = eg.Guest.JobTitle,
                EmploymentGrade = eg.Guest.EmploymentGrade,
                PhotoUrl = eg.Guest.PhotoUrl,
                DepartmentId = eg.Guest.Department != null ? (Guid?)eg.Guest.Department.PublicId : null,
                DepartmentName = eg.Guest.Department != null ? eg.Guest.Department.Name : null,
                MissionRoleId = eg.MissionRole != null ? (Guid?)eg.MissionRole.PublicId : null,
                MissionRoleName = eg.MissionRole != null ? eg.MissionRole.Name : null,
                Subgroup = eg.Subgroup,
                NominatedOn = eg.NominatedOn,
                NominatedByName = eg.NominatedByUser != null
                    ? (eg.NominatedByUser.FirstName + " " + eg.NominatedByUser.LastName).Trim()
                    : null,
                HrVerificationStatus = eg.HrVerificationStatus,
                HrVerifiedOn = eg.HrVerifiedOn,
                HrVerificationNote = eg.HrVerificationNote,
                PassportNumber = eg.Guest.PassportNumber,
                PassportExpiry = eg.Guest.PassportExpiry,
                VisaStatus = eg.VisaStatus,
                VisaRequired = eg.VisaRequired,
                InsuranceStatus = eg.InsuranceStatus,
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Fills the derived warnings. Computed here rather than stored so they can
    /// never go stale — a renewed passport or a cancelled mission changes the
    /// answer with no write anywhere.
    /// </summary>
    private async Task ApplyFlagsAsync(List<NominationResponse> roster, Event mission, CancellationToken ct)
    {
        if (roster.Count == 0) return;

        var reference = mission.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var warnFrom = reference.AddMonths(PassportWarningMonths);

        foreach (var row in roster)
        {
            if (string.IsNullOrWhiteSpace(row.PassportNumber) || !row.PassportExpiry.HasValue)
            {
                row.Flags.PassportMissing = true;
                continue;
            }

            if (row.PassportExpiry.Value < reference) row.Flags.PassportExpired = true;
            else if (row.PassportExpiry.Value < warnFrom) row.Flags.PassportExpiringSoon = true;
        }

        // Overlap: any other mission this person is on whose dates intersect.
        // Missions with no dates cannot overlap and are left out.
        if (!mission.StartDate.HasValue || !mission.EndDate.HasValue) return;

        var personIds = roster.Select(r => r.PersonId).ToList();
        var overlaps = await _unitOfWork.EventGuests.QueryNoTracking()
            .Where(eg => eg.EventId != mission.Id
                      && personIds.Contains(eg.Guest.PublicId)
                      && eg.Event.StartDate != null && eg.Event.EndDate != null
                      && eg.Event.StartDate <= mission.EndDate
                      && eg.Event.EndDate >= mission.StartDate)
            .Select(eg => new { PersonId = eg.Guest.PublicId, eg.Event.Title })
            .ToListAsync(ct);

        if (overlaps.Count == 0) return;

        var byPerson = overlaps.GroupBy(o => o.PersonId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Title).Distinct().ToList());

        foreach (var row in roster.Where(r => byPerson.ContainsKey(r.PersonId)))
        {
            row.Flags.OverlappingMission = true;
            row.Flags.OverlappingMissions = byPerson[row.PersonId];
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is Microsoft.Data.SqlClient.SqlException sql
           && (sql.Number == 2601 || sql.Number == 2627);
}

/// <summary>Lets a validation failure built as one ApiResponse be returned as
/// another, so the role check can be shared between endpoints returning
/// different payloads.</summary>
internal static class ApiResponseCastExtensions
{
    public static ApiResponse<T> As<T>(this ApiResponse<bool> source) => new()
    {
        Success = source.Success,
        Message = source.Message,
        Errors = source.Errors,
        ErrorCode = source.ErrorCode,
        StatusCode = source.StatusCode,
    };
}
