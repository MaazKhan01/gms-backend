using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.UserAccess;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

public class UserAccessService(IUnitOfWork _unitOfWork) : IUserAccessService
{
    public async Task<ApiResponse<UserModuleAccessResponse>> GetUserModuleAccessAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Include(u => u.ModuleGrants)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return ApiResponse<UserModuleAccessResponse>.NotFoundResponse("User not found");

        return ApiResponse<UserModuleAccessResponse>.SuccessResponse(BuildResponse(user));
    }

    public async Task<ApiResponse<UserModuleAccessResponse>> SetUserModuleAccessAsync(
        Guid userId, SetModuleAccessRequest request, Guid adminId, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Include(u => u.ModuleGrants)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return ApiResponse<UserModuleAccessResponse>.NotFoundResponse("User not found");

        // Validate all requested slugs are known modules.
        var unknownSlugs = request.GrantedModules
            .Where(s => !ModuleDefinitions.ViewPermissionBySlug.ContainsKey(s))
            .ToList();
        if (unknownSlugs.Count > 0)
            return ApiResponse<UserModuleAccessResponse>.ErrorResponse(
                $"Unknown module slugs: {string.Join(", ", unknownSlugs)}");

        var existingGrants = user.ModuleGrants.ToDictionary(g => g.Module);
        var desired = request.GrantedModules.ToHashSet();

        // Upsert: add new grants.
        foreach (var slug in desired.Where(s => !existingGrants.ContainsKey(s)))
        {
            await _unitOfWork.UserModuleGrants.AddAsync(new UserModuleGrant
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Module = slug,
                IsGranted = true,
                GrantedBy = adminId,
                GrantedAt = DateTime.UtcNow,
            }, ct);
        }

        // Update existing — toggle IsGranted.
        foreach (var (slug, grant) in existingGrants)
        {
            var shouldBeGranted = desired.Contains(slug);
            if (grant.IsGranted != shouldBeGranted)
            {
                grant.IsGranted = shouldBeGranted;
                grant.GrantedBy = adminId;
                grant.GrantedAt = DateTime.UtcNow;
                _unitOfWork.UserModuleGrants.Update(grant);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // Reload grants so the response reflects the saved state.
        var fresh = await _unitOfWork.Users.Query()
            .Include(u => u.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Include(u => u.ModuleGrants)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        return ApiResponse<UserModuleAccessResponse>.SuccessResponse(BuildResponse(fresh), "Access updated");
    }

    private static UserModuleAccessResponse BuildResponse(User user)
    {
        // Collect permissions the user has natively through their role.
        var nativePerms = user.Role?.RolePermissions?
            .Where(rp => rp.Permission != null)
            .Select(rp => rp.Permission.Code)
            .ToHashSet() ?? new HashSet<string>();

        var grantedSlugs = user.ModuleGrants
            .Where(g => g.IsGranted)
            .Select(g => g.Module)
            .ToHashSet();

        var modules = ModuleDefinitions.All.Select(m => new ModuleAccessItem
        {
            Slug = m.Slug,
            DisplayName = m.DisplayName,
            ViewPermission = m.ViewPermission,
            IsNative = nativePerms.Contains(m.ViewPermission),
            IsGranted = grantedSlugs.Contains(m.Slug),
        }).ToList();

        return new UserModuleAccessResponse
        {
            UserId = user.Id,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Email = user.Email,
            RoleName = user.Role?.Name,
            Modules = modules,
        };
    }
}
