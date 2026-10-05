using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Group;
using GroupEntity = DomainPersistence.Entities.Group;

namespace Infrastructure.Services;

/// <summary>
/// Delegation sub-groups. Admin-managed reference data, a name and nothing more.
///
/// Renaming one is the main edit, and it propagates: the nomination screens read
/// the group through the FK, and <c>EventGuest.Subgroup</c> — the legacy string
/// the roster screens still group by — is re-mirrored here so the two cannot
/// drift apart.
/// </summary>
public class GroupService(IUnitOfWork _unitOfWork) : IGroupService
{
    private const int NameMaxLength = 100;

    public async Task<ApiResponse<List<GroupResponse>>> GetAllAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.Groups.QueryNoTracking()
            .OrderBy(g => g.Name)
            .Select(g => new GroupResponse
            {
                Id = g.PublicId,
                Name = g.Name,
                MemberCount = g.EventGuests.Count(eg => eg.IsDeleted != true),
            })
            .ToListAsync(ct);

        return ApiResponse<List<GroupResponse>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<GroupResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return ApiResponse<GroupResponse>.ErrorResponse("Group id is required.");

        var data = await _unitOfWork.Groups.QueryNoTracking()
            .Where(g => g.PublicId == id)
            .Select(g => new GroupResponse
            {
                Id = g.PublicId,
                Name = g.Name,
                MemberCount = g.EventGuests.Count(eg => eg.IsDeleted != true),
            })
            .FirstOrDefaultAsync(ct);

        return data == null
            ? ApiResponse<GroupResponse>.NotFoundResponse("Group not found.")
            : ApiResponse<GroupResponse>.SuccessResponse(data);
    }

    public async Task<ApiResponse<GroupResponse>> CreateAsync(
        CreateGroupRequest request, int userId, CancellationToken ct = default)
    {
        var name = request?.Name?.Trim();
        var error = ValidateName(name);
        if (error != null) return ApiResponse<GroupResponse>.ErrorResponse(error);

        if (await NameTakenAsync(name, null, ct))
            return ApiResponse<GroupResponse>.ConflictResponse(
                $"A group named '{name}' already exists.", "GROUP_NAME_CONFLICT");

        var entity = new GroupEntity { Name = name };
        if (userId != 0) entity.SetCreationAudit(userId);

        await _unitOfWork.Groups.AddAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<GroupResponse>.SuccessResponse(
            new GroupResponse { Id = entity.PublicId, Name = entity.Name, MemberCount = 0 },
            "Group created.");
    }

    public async Task<ApiResponse<GroupResponse>> UpdateAsync(
        Guid id, UpdateGroupRequest request, int userId, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return ApiResponse<GroupResponse>.ErrorResponse("Group id is required.");

        var name = request?.Name?.Trim();
        var error = ValidateName(name);
        if (error != null) return ApiResponse<GroupResponse>.ErrorResponse(error);

        var entity = await _unitOfWork.Groups.Query().FirstOrDefaultAsync(g => g.PublicId == id, ct);
        if (entity == null)
            return ApiResponse<GroupResponse>.NotFoundResponse("Group not found.");

        if (await NameTakenAsync(name, entity.Id, ct))
            return ApiResponse<GroupResponse>.ConflictResponse(
                $"A group named '{name}' already exists.", "GROUP_NAME_CONFLICT");

        entity.Name = name;
        if (userId != 0) entity.SetUpdateAudit(userId);
        _unitOfWork.Groups.Update(entity);

        // Re-mirror the legacy string on every member. Readiness, On-Mission Ops
        // and Incidents group by EventGuest.Subgroup, so a rename that stopped at
        // the lookup row would leave those three screens showing the old name
        // indefinitely — the rename would look like it had done nothing.
        var members = await _unitOfWork.EventGuests.Query()
            .Where(eg => eg.GroupId == entity.Id)
            .ToListAsync(ct);
        foreach (var m in members) m.Subgroup = name;

        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<GroupResponse>.SuccessResponse(
            new GroupResponse { Id = entity.PublicId, Name = entity.Name, MemberCount = members.Count },
            "Group updated.");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(Guid id, int userId, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return ApiResponse<bool>.ErrorResponse("Group id is required.");

        var entity = await _unitOfWork.Groups.Query().FirstOrDefaultAsync(g => g.PublicId == id, ct);
        if (entity == null)
            return ApiResponse<bool>.NotFoundResponse("Group not found.");

        // The FK is RESTRICT, so this would fail at the database anyway — saying
        // how many delegates are in the way is more use than a constraint error.
        var members = await _unitOfWork.EventGuests.QueryNoTracking()
            .CountAsync(eg => eg.GroupId == entity.Id, ct);
        if (members > 0)
            return ApiResponse<bool>.ConflictResponse(
                $"Cannot delete — {members} delegate(s) are still in this group.", "GROUP_IN_USE");

        entity.MarkAsDeleted(userId);
        _unitOfWork.Groups.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.SuccessResponse(true, "Group deleted.");
    }

    private static string ValidateName(string name) =>
        string.IsNullOrWhiteSpace(name) ? "Name is required."
        : name.Length > NameMaxLength ? $"Name must be {NameMaxLength} characters or fewer."
        : null;

    // excludeId lets an update keep its own name.
    private Task<bool> NameTakenAsync(string name, int? excludeId, CancellationToken ct) =>
        _unitOfWork.Groups.QueryNoTracking()
            .AnyAsync(g => g.Name == name && (excludeId == null || g.Id != excludeId), ct);
}
