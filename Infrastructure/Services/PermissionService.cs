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
using Core.ViewModel.Permission;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<PermissionService> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<ApiResponse<List<PermissionResponse>>> GetAllPermissionsAsync(CancellationToken ct = default)
    {
        try
        {
            var permissions = await _unitOfWork.Permissions.Query()
                .OrderBy(p => p.Module).ThenBy(p => p.Name)
                .ToListAsync(ct);

            return ApiResponse<List<PermissionResponse>>.SuccessResponse(
                _mapper.Map<List<PermissionResponse>>(permissions), $"Retrieved {permissions.Count} permissions");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions");
            return ApiResponse<List<PermissionResponse>>.ServerErrorResponse("An error occurred while retrieving permissions");
        }
    }

    public async Task<ApiResponse<List<PermissionResponse>>> GetPermissionsByModuleAsync(string module, CancellationToken ct = default)
    {
        try
        {
            var permissions = await _unitOfWork.Permissions.Query()
                .Where(p => p.Module.ToLower() == module.ToLower())
                .OrderBy(p => p.Name)
                .ToListAsync(ct);

            return ApiResponse<List<PermissionResponse>>.SuccessResponse(
                _mapper.Map<List<PermissionResponse>>(permissions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions for module {Module}", module);
            return ApiResponse<List<PermissionResponse>>.ServerErrorResponse("An error occurred while retrieving permissions");
        }
    }

    public async Task<ApiResponse<List<string>>> GetModulesAsync(CancellationToken ct = default)
    {
        try
        {
            var modules = await _unitOfWork.Permissions.Query()
                .Select(p => p.Module).Distinct().OrderBy(m => m)
                .ToListAsync(ct);

            return ApiResponse<List<string>>.SuccessResponse(modules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving modules");
            return ApiResponse<List<string>>.ServerErrorResponse("An error occurred while retrieving modules");
        }
    }

    public async Task<ApiResponse<PermissionResponse>> CreatePermissionAsync(CreatePermissionRequest request, Guid currentUserId, CancellationToken ct = default)
    {
        try
        {
            var exists = await _unitOfWork.Permissions.Query().AnyAsync(p => p.Code == request.Code, ct);
            if (exists)
                return ApiResponse<PermissionResponse>.ConflictResponse("A permission with this code already exists");

            var permission = new Permission
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Code = request.Code,
                Module = request.Module,
                Description = request.Description,
                CreatedBy = currentUserId,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Permissions.AddAsync(permission, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<PermissionResponse>.SuccessResponse(
                _mapper.Map<PermissionResponse>(permission), "Permission created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating permission");
            return ApiResponse<PermissionResponse>.ServerErrorResponse("An error occurred while creating the permission");
        }
    }
}
