using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.User;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

public class UserService(
    IUnitOfWork _unitOfWork,
    IMapper _mapper,
    IEmailService _emailService,
    IConfiguration _configuration,
    ILogger<UserService> _logger) : IUserService
{
    private string FrontendUrl => _configuration.GetValue<string>("FrontendUrl") ?? "http://localhost:5173";


    public async Task<ApiResponse<UserResponse>> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        try
        {
            var existingUser = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower() && u.IsDeleted != true, ct);

            if (existingUser != null)
                return ApiResponse<UserResponse>.ConflictResponse("Email already exists");

            var role = await _unitOfWork.Roles.Query().FirstOrDefaultAsync(r => r.PublicId == request.RoleId, ct);
            if (role == null)
                return ApiResponse<UserResponse>.NotFoundResponse("Role not found");

            var user = new User
            {
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
                .FirstOrDefaultAsync(u => u.PublicId == id && u.IsDeleted != true, ct);

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
                .Take(request.PageSize).Include(u => u.Role)
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

    public async Task<ApiResponse<UserResponse>> UpdateUserAsync(Guid id, UpdateUserRequest request, int currentUserId, CancellationToken ct = default)
    {
        try
        {
            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.PublicId == id && u.IsDeleted != true, ct);

            if (user == null)
                return ApiResponse<UserResponse>.NotFoundResponse("User not found");

            if (request.RoleId.HasValue)
            {
                var role = await _unitOfWork.Roles.Query().FirstOrDefaultAsync(r => r.PublicId == request.RoleId.Value, ct);
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

    public async Task<ApiResponse<bool>> DeleteUserAsync(Guid id, int currentUserId, CancellationToken ct = default)
    {
        try
        {
            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.PublicId == id && u.IsDeleted != true, ct);

            if (user == null)
                return ApiResponse<bool>.NotFoundResponse("User not found");

            if (user.Id == currentUserId)
                return ApiResponse<bool>.ErrorResponse("Cannot delete your own account");

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
                .FirstOrDefaultAsync(u => u.PublicId == userId && u.IsDeleted != true, ct);

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

    public async Task<ApiResponse<UserResponse>> InviteUserAsync(InviteUserRequest request, int inviterId, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return ApiResponse<UserResponse>.ErrorResponse("Email is required");

            var email = request.Email.Trim().ToLowerInvariant();

            // Deleted users are soft-deleted (still queryable — Users has no
            // IsDeleted query filter), so exclude them here or their email would
            // stay blocked forever. Mirrors the DB's filtered unique index below.
            if (await _unitOfWork.Users.Query().AnyAsync(u => u.Email == email && u.IsDeleted != true, ct))
                return ApiResponse<UserResponse>.ConflictResponse("An account with this email already exists");

            var role = await _unitOfWork.Roles.Query().FirstOrDefaultAsync(r => r.PublicId == request.RoleId, ct);
            if (role == null)
                return ApiResponse<UserResponse>.NotFoundResponse("Role not found");

            var user = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = email,
                Phone = request.Phone,
                RoleId = role.Id,
                PasswordHash = null,
                IsActive = false,
                InviteToken = Guid.NewGuid(),
                InviteSentAt = DateTime.UtcNow,
            };
            user.SetCreationAudit(inviterId);
            await _unitOfWork.Users.AddAsync(user, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            if (role.Code == Roles.DRIVER && request.DriverProfile != null)
            {
                var d = request.DriverProfile;
               
                var nationalityId = d.NationalityId.HasValue
                    ? (await _unitOfWork.Nationalities.GetByPublicIdAsync(d.NationalityId.Value, ct))?.Id
                    : null;

                var profile = new DriverProfile
                {
                    UserId = user.Id,
                    DriverType = d.DriverType,
                    LicenseNumber = d.LicenseNumber,
                    LicenseExpiry = d.LicenseExpiry,
                    NationalityId = nationalityId,
                    PhotoUrl = d.PhotoUrl,
                };
                profile.SetCreationAudit(inviterId);
                await _unitOfWork.DriverProfiles.AddAsync(profile, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            var emailSent = await TrySendInviteEmailAsync(user, role.Name, ct);

            var created = await _unitOfWork.Users.Query()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == user.Id, ct);

            var response = _mapper.Map<UserResponse>(created);
            response.InviteEmailSent = emailSent;
            var message = emailSent
                ? "Invite sent"
                : "User created, but the invite email could not be sent — use Resend Invite to try again.";
            return ApiResponse<UserResponse>.SuccessResponse(response, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inviting user");
            return ApiResponse<UserResponse>.ServerErrorResponse("An error occurred while inviting the user");
        }
    }

    public async Task<ApiResponse<PaginatedResponse<PendingUserResponse>>> GetPendingUsersAsync(PagedRequest request, CancellationToken ct = default)
    {
        try
        {
            var query = _unitOfWork.Users.Query()
                .Where(u => u.IsDeleted != true && !u.IsActive && u.InviteToken != null);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLower();
                query = query.Where(u =>
                    u.FirstName.ToLower().Contains(term) ||
                    u.LastName.ToLower().Contains(term) ||
                    u.Email.ToLower().Contains(term));
            }

            var total = await query.CountAsync(ct);
            var users = await query
                .OrderByDescending(u => u.InviteSentAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Include(u => u.Role)
                .ToListAsync(ct);

            var mapped = users.Select(u => new PendingUserResponse
            {
                Id = u.PublicId,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                RoleName = u.Role?.Name,
                InviteSentAt = u.InviteSentAt,
            }).ToList();

            var paged = new PaginatedResponse<PendingUserResponse>(mapped, total, request.PageNumber, request.PageSize);
            return ApiResponse<PaginatedResponse<PendingUserResponse>>.SuccessResponse(paged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending users");
            return ApiResponse<PaginatedResponse<PendingUserResponse>>.ServerErrorResponse("An error occurred while retrieving pending users");
        }
    }

    public async Task<ApiResponse<bool>> ResendInviteAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var user = await _unitOfWork.Users.Query()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.PublicId == id && u.IsDeleted != true && !u.IsActive, ct);
            if (user == null)
                return ApiResponse<bool>.NotFoundResponse("Pending invite not found");

            user.InviteToken = Guid.NewGuid();
            user.InviteSentAt = DateTime.UtcNow;
            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);

            var emailSent = await TrySendInviteEmailAsync(user, user.Role?.Name, ct);

            return emailSent
                ? ApiResponse<bool>.SuccessResponse(true, "Invite resent")
                : ApiResponse<bool>.ServerErrorResponse("Could not send the invite email — check the server logs for details.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending invite {UserId}", id);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while resending the invite");
        }
    }

    public async Task<ApiResponse<bool>> AdminSetPasswordAsync(Guid id, AdminSetPasswordRequest request, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword != request.ConfirmPassword)
                return ApiResponse<bool>.ErrorResponse("New password and confirm password do not match");

            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.PublicId == id && u.IsDeleted != true, ct);
            if (user == null)
                return ApiResponse<bool>.NotFoundResponse("User not found");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "Password updated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting password for user {UserId}", id);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while setting the password");
        }
    }

    public async Task<ApiResponse<InviteDetailsResponse>> GetInviteByTokenAsync(Guid token, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.InviteToken == token && u.IsDeleted != true && !u.IsActive, ct);
        if (user == null)
            return ApiResponse<InviteDetailsResponse>.NotFoundResponse("Invite not found or already accepted");

        return ApiResponse<InviteDetailsResponse>.SuccessResponse(new InviteDetailsResponse
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            RoleName = user.Role?.Name,
        });
    }

    public async Task<ApiResponse<bool>> AcceptInviteAsync(Guid token, AcceptInviteRequest request, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password != request.ConfirmPassword)
                return ApiResponse<bool>.ErrorResponse("Password and confirm password do not match");

            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.InviteToken == token && u.IsDeleted != true && !u.IsActive, ct);
            if (user == null)
                return ApiResponse<bool>.NotFoundResponse("Invite not found or already accepted");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            user.IsActive = true;
            user.InviteToken = null;
            user.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "Account activated — you can now log in");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting invite");
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while accepting the invite");
        }
    }

    // Awaited, not a detached Task.Run — a fire-and-forget task here outlives
    // the request's DI scope (IEmailService is Scoped) and isn't tied to the
    // request lifetime at all, so on a process recycle/idle-shutdown it can be
    // abandoned mid-send with no exception ever thrown and nothing logged.
    // Awaiting it keeps the send inside the request, and any failure is both
    // logged at Error level and reflected in the response message so it's
    // never silently lost.
    private async Task<bool> TrySendInviteEmailAsync(User user, string roleName, CancellationToken ct)
    {
        var acceptUrl = $"{FrontendUrl}/?screen=userInvite&token={user.InviteToken}";
        try
        {
            await _emailService.SendUserInviteAsync(user.Email, user.FirstName, roleName, acceptUrl, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not send invite email to {Email}", user.Email);
            return false;
        }
    }
}
