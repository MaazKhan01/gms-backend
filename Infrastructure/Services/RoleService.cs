using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Role;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

public class RoleService : IRoleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<RoleService> _logger;

    public RoleService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<RoleService> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<ApiResponse<RoleResponse>> CreateRoleAsync(CreateRoleRequest request, int currentUserId, CancellationToken ct = default)
    {
        try
        {
            var exists = await _unitOfWork.Roles.Query().AnyAsync(r => r.Code == request.Code, ct);
            if (exists)
                return ApiResponse<RoleResponse>.ConflictResponse("A role with this code already exists");

            List<Permission> permissions = new();
            if (request.PermissionIds?.Any() == true)
            {
                permissions = await _unitOfWork.Permissions.Query()
                    .Where(p => request.PermissionIds.Contains(p.PublicId)).ToListAsync(ct);
                if (permissions.Count != request.PermissionIds.Count)
                    return ApiResponse<RoleResponse>.ErrorResponse("One or more permission IDs are invalid");
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var role = new Role
                {
                    Name = request.Name,
                    Code = request.Code,
                    Description = request.Description,
                    // Default on: a new role is a portal role unless told otherwise.
                    PortalAccess = request.PortalAccess ?? true,
                    CreatedBy = currentUserId,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Roles.AddAsync(role, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                if (permissions.Count > 0)
                {
                    foreach (var permission in permissions)
                        await _unitOfWork.RolePermissions.AddAsync(new RolePermission
                        {
                            RoleId = role.Id,
                            PermissionId = permission.Id,
                            CreatedBy = currentUserId,
                            CreatedAt = DateTime.UtcNow
                        }, ct);

                    await _unitOfWork.SaveChangesAsync(ct);
                }

                await _unitOfWork.CommitTransactionAsync();

                var created = await _unitOfWork.Roles.Query()
                    .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                    .FirstOrDefaultAsync(r => r.Id == role.Id, ct);

                return ApiResponse<RoleResponse>.SuccessResponse(_mapper.Map<RoleResponse>(created), "Role created successfully");
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role");
            return ApiResponse<RoleResponse>.ServerErrorResponse("An error occurred while creating the role");
        }
    }

    public async Task<ApiResponse<RoleResponse>> GetRoleByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var role = await _unitOfWork.Roles.Query()
                .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(r => r.PublicId == id, ct);

            if (role == null)
                return ApiResponse<RoleResponse>.NotFoundResponse("Role not found");

            var userCount = await _unitOfWork.Users.Query().CountAsync(u => u.RoleId == role.Id && u.IsDeleted != true, ct);
            var response = _mapper.Map<RoleResponse>(role);
            response.UserCount = userCount;

            return ApiResponse<RoleResponse>.SuccessResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role {RoleId}", id);
            return ApiResponse<RoleResponse>.ServerErrorResponse("An error occurred while retrieving the role");
        }
    }

    public async Task<ApiResponse<List<RoleResponse>>> GetAllRolesAsync(CancellationToken ct = default)
    {
        try
        {
            var roles = await _unitOfWork.Roles.Query()
                .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                .OrderBy(r => r.Name)
                .ToListAsync(ct);

            var roleIds = roles.Select(r => r.Id).ToList();
            var userCounts = await _unitOfWork.Users.Query()
                .Where(u => u.RoleId != null && roleIds.Contains(u.RoleId.Value) && u.IsDeleted != true)
                .GroupBy(u => u.RoleId!.Value)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var response = _mapper.Map<List<RoleResponse>>(roles);
            for (int i = 0; i < roles.Count; i++)
                response[i].UserCount = userCounts.FirstOrDefault(x => x.RoleId == roles[i].Id)?.Count ?? 0;

            return ApiResponse<List<RoleResponse>>.SuccessResponse(response, $"Retrieved {roles.Count} roles");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving roles");
            return ApiResponse<List<RoleResponse>>.ServerErrorResponse("An error occurred while retrieving roles");
        }
    }

    public async Task<ApiResponse<RoleResponse>> UpdateRoleAsync(Guid id, UpdateRoleRequest request, int currentUserId, CancellationToken ct = default)
    {
        try
        {
            var role = await _unitOfWork.Roles.Query()
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.PublicId == id, ct);

            if (role == null)
                return ApiResponse<RoleResponse>.NotFoundResponse("Role not found");

            List<Permission> permissions = new();
            if (request.PermissionIds?.Any() == true)
            {
                permissions = await _unitOfWork.Permissions.Query()
                    .Where(p => request.PermissionIds.Contains(p.PublicId)).ToListAsync(ct);
                if (permissions.Count != request.PermissionIds.Count)
                    return ApiResponse<RoleResponse>.ErrorResponse("One or more permission IDs are invalid");
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                role.Name = request.Name;
                role.Description = request.Description;
                if (request.PortalAccess.HasValue) role.PortalAccess = request.PortalAccess.Value;
                _unitOfWork.Roles.Update(role);
                await _unitOfWork.SaveChangesAsync(ct);

                if (request.PermissionIds != null)
                {
                    var existing = await _unitOfWork.RolePermissions.Query()
                        .Where(rp => rp.RoleId == role.Id).ToListAsync(ct);
                    _unitOfWork.RolePermissions.RemoveRange(existing);
                    await _unitOfWork.SaveChangesAsync(ct);

                    foreach (var permission in permissions)
                        await _unitOfWork.RolePermissions.AddAsync(new RolePermission
                        {
                            RoleId = role.Id,
                            PermissionId = permission.Id,
                            CreatedBy = currentUserId,
                            CreatedAt = DateTime.UtcNow
                        }, ct);

                    await _unitOfWork.SaveChangesAsync(ct);
                }

                await _unitOfWork.CommitTransactionAsync();

                var updated = await _unitOfWork.Roles.Query()
                    .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                    .FirstOrDefaultAsync(r => r.PublicId == id, ct);

                var userCount = await _unitOfWork.Users.Query().CountAsync(u => u.RoleId == role.Id && u.IsDeleted != true, ct);
                var response = _mapper.Map<RoleResponse>(updated);
                response.UserCount = userCount;

                return ApiResponse<RoleResponse>.SuccessResponse(response, "Role updated successfully");
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role {RoleId}", id);
            return ApiResponse<RoleResponse>.ServerErrorResponse("An error occurred while updating the role");
        }
    }

    public async Task<ApiResponse<bool>> DeleteRoleAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var role = await _unitOfWork.Roles.Query().FirstOrDefaultAsync(r => r.PublicId == id, ct);
            if (role == null)
                return ApiResponse<bool>.NotFoundResponse("Role not found");

            var userCount = await _unitOfWork.Users.Query().CountAsync(u => u.RoleId == role.Id && u.IsDeleted != true, ct);
            if (userCount > 0)
                return ApiResponse<bool>.ConflictResponse($"Cannot delete role — assigned to {userCount} user(s)");

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var perms = await _unitOfWork.RolePermissions.Query().Where(rp => rp.RoleId == role.Id).ToListAsync(ct);
                _unitOfWork.RolePermissions.RemoveRange(perms);
                await _unitOfWork.SaveChangesAsync(ct);

                _unitOfWork.Roles.Remove(role);
                await _unitOfWork.SaveChangesAsync(ct);
                await _unitOfWork.CommitTransactionAsync();

                return ApiResponse<bool>.SuccessResponse(true, "Role deleted successfully");
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role {RoleId}", id);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the role");
        }
    }
}
