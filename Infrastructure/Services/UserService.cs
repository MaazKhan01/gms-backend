using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.User;
using DomainPersistence.Entities;
using DomainPersistence.Enums;

namespace Infrastructure.Services;

public class UserService(
    IUnitOfWork _unitOfWork,
    IMapper _mapper,
    IEmailService _emailService,
    IBackgroundJobClient _backgroundJobClient,
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

            // Case-insensitive: a role row seeded/edited as "Driver" must still be
            // recognised, or the driver silently gets no profile and then never
            // shows up in any driver lookup.
            var isDriver = string.Equals(role.Code, Roles.DRIVER, StringComparison.OrdinalIgnoreCase);
            if (isDriver)
            {
                if (request.DriverProfile is null)
                    return ApiResponse<UserResponse>.ErrorResponse("Driver details are required for the driver role");

                var d = request.DriverProfile;

                var nationalityId = d.NationalityId.HasValue
                    ? (await _unitOfWork.Nationalities.GetByPublicIdAsync(d.NationalityId.Value, ct))?.Id
                    : null;

                var (vehicleError, assignedVehicleId) = await ResolveDriverVehicleAsync(d, ct);
                if (vehicleError != null)
                    return ApiResponse<UserResponse>.ErrorResponse(vehicleError);

                var profile = new DriverProfile
                {
                    // Navigation, not UserId: the user row hasn't been inserted yet,
                    // so its Id is still 0 — EF fills the FK when it saves both.
                    User = user,
                    DriverType = d.DriverType,
                    AssignedVehicleId = assignedVehicleId,
                    LicenseNumber = d.LicenseNumber,
                    LicenseExpiry = d.LicenseExpiry,
                    NationalityId = nationalityId,
                    PhotoUrl = d.PhotoUrl,
                };
                profile.SetCreationAudit(inviterId);
                await _unitOfWork.DriverProfiles.AddAsync(profile, ct);
            }

            // One SaveChanges for user + driver profile. Two separate saves left a
            // committed, invite-less account behind whenever the profile insert
            // failed: the catch below turned it into a 500 *after* the user row was
            // already permanent, and the email is sent below that point — so a
            // driver would exist with no invite mail and nothing to retry from.
            await _unitOfWork.SaveChangesAsync(ct);

            // ponytail: the ACS send was the bulk of this request's latency (network
            // POST). Hangfire owns it now — same pattern as the bulk imports — so the
            // admin gets the created user back as soon as the row is committed.
            // Trade-off: we can no longer report a send failure inline, so
            // InviteEmailSent stays true and a failed send surfaces as the invite
            // sitting in the Pending list (Resend Invite retries it). Hangfire also
            // retries the job itself. Await it again only if inline confirmation
            // matters more than the response time.
            _backgroundJobClient.Enqueue<IEmailService>(x => x.SendUserInviteAsync(
                user.Email, user.FirstName, role.Name, BuildInviteAcceptUrl(user.InviteToken), CancellationToken.None));

            // No reload: PublicId is populated by SaveChanges (newid() default is
            // value-generated-on-add) and Role is the row we already fetched.
            user.Role = role;
            var response = _mapper.Map<UserResponse>(user);
            return ApiResponse<UserResponse>.SuccessResponse(response, "Invite sent");
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

            var (emailSent, emailError) = await TrySendInviteEmailAsync(user, user.Role?.Name, ct);

            return emailSent
                ? ApiResponse<bool>.SuccessResponse(true, "Invite resent")
                : ApiResponse<bool>.ServerErrorResponse($"Could not send the invite email. {emailError}".Trim());
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

    // Resolves the driver's permanent vehicle, enforcing the crossed pairing:
    // an Open driver roams and gets a Fixed car of their own; a Fixed driver is tied
    // to a guest and takes an Open pool car per trip via Transport.VehicleId, so no
    // permanent car is stored for them at all.
    //
    // Returns (error, internal vehicle id). Error non-null means reject the invite.
    private async Task<(string Error, int? VehicleId)> ResolveDriverVehicleAsync(
        DriverProfileInput input, CancellationToken ct)
    {
        var isOpenDriver = input.DriverType == DomainPersistence.Enums.DriverType.Open;

        if (input.AssignedVehicleId is not { } vehiclePublicId || vehiclePublicId == Guid.Empty)
            return isOpenDriver
                ? ("A vehicle is required for an open driver", null)
                : (null, null);

        if (!isOpenDriver)
            return ("Only an open driver can be assigned a vehicle — a fixed driver is given one per trip.", null);

        var vehicle = await _unitOfWork.Vehicles.Query()
            .Where(v => v.PublicId == vehiclePublicId)
            .Select(v => new { v.Id, v.UsageType })
            .FirstOrDefaultAsync(ct);

        if (vehicle == null)
            return ("Vehicle not found", null);

        if (vehicle.UsageType != VehicleUsageType.Fixed)
            return ("An open driver can only be assigned a fixed vehicle", null);

        // Fixed cars take exactly one driver. Checked here as well as by the unique
        // index so the admin gets this sentence instead of a DbUpdateException; the
        // index is what actually holds under two concurrent invites.
        var alreadyHeld = await _unitOfWork.DriverProfiles.Query()
            .AnyAsync(p => p.AssignedVehicleId == vehicle.Id, ct);
        if (alreadyHeld)
            return ("This vehicle is already assigned to another driver", null);

        return (null, vehicle.Id);
    }

    private string BuildInviteAcceptUrl(Guid? inviteToken) => $"{FrontendUrl}/?screen=userInvite&token={inviteToken}";

    private async Task<(bool Sent, string Error)> TrySendInviteEmailAsync(User user, string roleName, CancellationToken ct)
    {
        var acceptUrl = BuildInviteAcceptUrl(user.InviteToken);
        try
        {
            await _emailService.SendUserInviteAsync(user.Email, user.FirstName, roleName, acceptUrl, ct);
            return (true, null);
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogError(ex, "Could not send invite email to {Email} — ACS {Status} {ErrorCode}",
                user.Email, ex.Status, ex.ErrorCode);
            return (false, $"Email provider rejected the send ({ex.Status} {ex.ErrorCode}): {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not send invite email to {Email}", user.Email);
            return (false, ex.Message);
        }
    }
}
