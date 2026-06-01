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
using Core.ViewModel.User;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<UserService> _logger;

    public UserService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<UserService> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<ApiResponse<UserResponse>> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        try
        {
            var existingUser = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower() && u.IsDeleted != true, ct);

            if (existingUser != null)
                return ApiResponse<UserResponse>.ConflictResponse("Email already exists");

            var role = await _unitOfWork.Roles.Query().FirstOrDefaultAsync(r => r.Id == request.RoleId, ct);
            if (role == null)
                return ApiResponse<UserResponse>.NotFoundResponse("Role not found");

            var user = new User
            {
                Id = Guid.NewGuid(),
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email.ToLower().Trim(),
                Phone = request.Phone,
                RoleId = role.Id,
                PasswordHash = !string.IsNullOrEmpty(request.Password)
                    ? BCrypt.Net.BCrypt.HashPassword(request.Password)
                    : null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _unitOfWork.Users.AddAsync(user, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var created = await _unitOfWork.Users.Query()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == user.Id, ct);

            return ApiResponse<UserResponse>.SuccessResponse(_mapper.Map<UserResponse>(created), "User created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return ApiResponse<UserResponse>.ServerErrorResponse("An error occurred while creating the user");
        }
    }

    public async Task<ApiResponse<UserResponse>> GetUserByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var user = await _unitOfWork.Users.Query()
                .Include(u => u.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.Id == id && u.IsDeleted != true, ct);

            if (user == null)
                return ApiResponse<UserResponse>.NotFoundResponse("User not found");

            return ApiResponse<UserResponse>.SuccessResponse(_mapper.Map<UserResponse>(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", id);
            return ApiResponse<UserResponse>.ServerErrorResponse("An error occurred while retrieving the user");
        }
    }

    public async Task<ApiResponse<PaginatedResponse<UserResponse>>> GetUsersAsync(PagedRequest request, CancellationToken ct = default)
    {
        try
        {
            var query = _unitOfWork.Users.Query().Where(u => u.IsDeleted != true);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLower();
                query = query.Where(u =>
                    u.FirstName.ToLower().Contains(term) ||
                    u.LastName.ToLower().Contains(term) ||
                    u.Email.ToLower().Contains(term));
            }

            var totalCount = await query.CountAsync(ct);

            query = (request.SortBy?.ToLower()) switch
            {
                "lastname" => request.SortDescending ? query.OrderByDescending(u => u.LastName) : query.OrderBy(u => u.LastName),
                "email" => request.SortDescending ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
                "createdat" => request.SortDescending ? query.OrderByDescending(u => u.CreatedAt) : query.OrderBy(u => u.CreatedAt),
                _ => request.SortDescending ? query.OrderByDescending(u => u.FirstName) : query.OrderBy(u => u.FirstName)
            };

            var users = await query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(ct);

            var paged = new PaginatedResponse<UserResponse>(
                _mapper.Map<List<UserResponse>>(users), totalCount, request.PageNumber, request.PageSize);

            return ApiResponse<PaginatedResponse<UserResponse>>.SuccessResponse(paged, $"Retrieved {users.Count} users");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return ApiResponse<PaginatedResponse<UserResponse>>.ServerErrorResponse("An error occurred while retrieving users");
        }
    }

    public async Task<ApiResponse<UserResponse>> UpdateUserAsync(Guid id, UpdateUserRequest request, Guid currentUserId, CancellationToken ct = default)
    {
        try
        {
            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Id == id && u.IsDeleted != true, ct);

            if (user == null)
                return ApiResponse<UserResponse>.NotFoundResponse("User not found");

            if (request.RoleId.HasValue)
            {
                var role = await _unitOfWork.Roles.Query().FirstOrDefaultAsync(r => r.Id == request.RoleId.Value, ct);
                if (role == null)
                    return ApiResponse<UserResponse>.NotFoundResponse("Role not found");
                user.RoleId = role.Id;
            }

            user.FirstName = request.FirstName ?? user.FirstName;
            user.LastName = request.LastName ?? user.LastName;
            user.Phone = request.Phone ?? user.Phone;
            if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;
            user.UpdatedBy = currentUserId;
            user.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<UserResponse>.SuccessResponse(_mapper.Map<UserResponse>(user), "User updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);
            return ApiResponse<UserResponse>.ServerErrorResponse("An error occurred while updating the user");
        }
    }

    public async Task<ApiResponse<bool>> DeleteUserAsync(Guid id, Guid currentUserId, CancellationToken ct = default)
    {
        try
        {
            if (id == currentUserId)
                return ApiResponse<bool>.ErrorResponse("Cannot delete your own account");

            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Id == id && u.IsDeleted != true, ct);

            if (user == null)
                return ApiResponse<bool>.NotFoundResponse("User not found");

            user.IsDeleted = true;
            user.DeletedBy = currentUserId;
            user.DeletedAt = DateTime.UtcNow;

            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "User deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the user");
        }
    }

    public async Task<ApiResponse<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        try
        {
            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsDeleted != true, ct);

            if (user == null)
                return ApiResponse<bool>.NotFoundResponse("User not found");

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                return ApiResponse<bool>.UnauthorizedResponse("Current password is incorrect");

            if (request.NewPassword != request.ConfirmPassword)
                return ApiResponse<bool>.ErrorResponse("New password and confirm password do not match");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "Password changed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", userId);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while changing the password");
        }
    }
}
