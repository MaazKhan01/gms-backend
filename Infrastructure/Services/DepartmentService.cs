using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Department;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

/// <summary>Departments a person belongs to. Admin-managed reference data; a
/// Department Head nominates only from their own.</summary>
public class DepartmentService(IUnitOfWork _unitOfWork) : IDepartmentService
{
    private const int NameMaxLength = 150;

    public async Task<ApiResponse<List<DepartmentResponse>>> GetAllAsync(CancellationToken ct = default)
    {
        var data = await _unitOfWork.Departments.QueryNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentResponse
            {
                Id = d.PublicId,
                Name = d.Name,
                NameAr = d.NameAr,
                MemberCount = d.Guests.Count(g => g.IsDeleted != true),
            })
            .ToListAsync(ct);

        return ApiResponse<List<DepartmentResponse>>.SuccessResponse(data);
    }

    public async Task<ApiResponse<DepartmentResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return ApiResponse<DepartmentResponse>.ErrorResponse("Department id is required.");

        var data = await _unitOfWork.Departments.QueryNoTracking()
            .Where(d => d.PublicId == id)
            .Select(d => new DepartmentResponse
            {
                Id = d.PublicId,
                Name = d.Name,
                NameAr = d.NameAr,
                MemberCount = d.Guests.Count(g => g.IsDeleted != true),
            })
            .FirstOrDefaultAsync(ct);

        return data == null
            ? ApiResponse<DepartmentResponse>.NotFoundResponse("Department not found.")
            : ApiResponse<DepartmentResponse>.SuccessResponse(data);
    }

    public async Task<ApiResponse<DepartmentResponse>> CreateAsync(
        CreateDepartmentRequest request, int userId, CancellationToken ct = default)
    {
        var name = request?.Name?.Trim();
        var error = ValidateName(name);
        if (error != null) return ApiResponse<DepartmentResponse>.ErrorResponse(error);

        if (await NameTakenAsync(name, null, ct))
            return ApiResponse<DepartmentResponse>.ConflictResponse(
                $"A department named '{name}' already exists.", "DEPARTMENT_NAME_CONFLICT");

        var entity = new Department { Name = name, NameAr = request.NameAr?.Trim() };
        if (userId != 0) entity.SetCreationAudit(userId);

        await _unitOfWork.Departments.AddAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<DepartmentResponse>.SuccessResponse(Map(entity, 0), "Department created.");
    }

    public async Task<ApiResponse<DepartmentResponse>> UpdateAsync(
        Guid id, UpdateDepartmentRequest request, int userId, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return ApiResponse<DepartmentResponse>.ErrorResponse("Department id is required.");

        var name = request?.Name?.Trim();
        var error = ValidateName(name);
        if (error != null) return ApiResponse<DepartmentResponse>.ErrorResponse(error);

        var entity = await _unitOfWork.Departments.Query().FirstOrDefaultAsync(d => d.PublicId == id, ct);
        if (entity == null)
            return ApiResponse<DepartmentResponse>.NotFoundResponse("Department not found.");

        if (await NameTakenAsync(name, entity.Id, ct))
            return ApiResponse<DepartmentResponse>.ConflictResponse(
                $"A department named '{name}' already exists.", "DEPARTMENT_NAME_CONFLICT");

        entity.Name = name;
        entity.NameAr = request.NameAr?.Trim();
        if (userId != 0) entity.SetUpdateAudit(userId);

        _unitOfWork.Departments.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        var members = await _unitOfWork.Guests.QueryNoTracking().CountAsync(g => g.DepartmentId == entity.Id, ct);
        return ApiResponse<DepartmentResponse>.SuccessResponse(Map(entity, members), "Department updated.");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(Guid id, int userId, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return ApiResponse<bool>.ErrorResponse("Department id is required.");

        var entity = await _unitOfWork.Departments.Query().FirstOrDefaultAsync(d => d.PublicId == id, ct);
        if (entity == null)
            return ApiResponse<bool>.NotFoundResponse("Department not found.");

        // Refuse rather than orphan: the FK is SetNull, so deleting would silently
        // blank the department on everyone in it.
        var members = await _unitOfWork.Guests.QueryNoTracking().CountAsync(g => g.DepartmentId == entity.Id, ct);
        if (members > 0)
            return ApiResponse<bool>.ConflictResponse(
                $"Cannot delete — {members} person(s) are still in this department.", "DEPARTMENT_IN_USE");

        entity.MarkAsDeleted(userId);
        _unitOfWork.Departments.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.SuccessResponse(true, "Department deleted.");
    }

    private static string ValidateName(string name) =>
        string.IsNullOrWhiteSpace(name) ? "Name is required."
        : name.Length > NameMaxLength ? $"Name must be {NameMaxLength} characters or fewer."
        : null;

    // excludeId lets an update keep its own name.
    private Task<bool> NameTakenAsync(string name, int? excludeId, CancellationToken ct) =>
        _unitOfWork.Departments.QueryNoTracking()
            .AnyAsync(d => d.Name == name && (excludeId == null || d.Id != excludeId), ct);

    private static DepartmentResponse Map(Department d, int memberCount) => new()
    {
        Id = d.PublicId,
        Name = d.Name,
        NameAr = d.NameAr,
        MemberCount = memberCount,
    };
}
