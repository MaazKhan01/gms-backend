using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Core.Constants;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.AccountRequest;
using Core.ViewModel.Common;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

public class AccountRequestService(
    IUnitOfWork _unitOfWork,
    IMapper _mapper,
    IEmailService _emailService,
    IConfiguration _configuration,
    ILogger<AccountRequestService> _logger) : IAccountRequestService
{
    private string LoginUrl => _configuration.GetValue<string>("FrontendUrl") ?? "http://localhost:5173";
    public async Task<ApiResponse<List<RequestableRoleResponse>>> GetRequestableRolesAsync(CancellationToken ct = default)
    {
        var roles = await _unitOfWork.Roles.QueryNoTracking()
            .Where(r => r.Code != Roles.ADMIN)
            .OrderBy(r => r.Name)
            .Select(r => new RequestableRoleResponse { Id = r.Id, Name = r.Name, Description = r.Description })
            .ToListAsync(ct);
        return ApiResponse<List<RequestableRoleResponse>>.SuccessResponse(roles);
    }

    public async Task<ApiResponse<AccountRequestResponse>> SubmitAsync(RegisterAccountRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return ApiResponse<AccountRequestResponse>.ErrorResponse("Email and password are required");

        var email = request.Email.Trim().ToLowerInvariant();

        // Resolve & validate the requested role (cannot request admin).
        Role requestedRole = null;
        if (request.RequestedRoleId is { } rid && rid != Guid.Empty)
        {
            requestedRole = await _unitOfWork.Roles.FindFirstOrDefaultAsync(r => r.Id == rid, ct);
            if (requestedRole == null)
                return ApiResponse<AccountRequestResponse>.ErrorResponse("Selected role does not exist");
            if (requestedRole.Code == Roles.ADMIN)
                return ApiResponse<AccountRequestResponse>.ErrorResponse("That role cannot be requested");
        }

        // Reject if a user already exists (ignore soft-delete filter to catch all).
        if (await _unitOfWork.Users.Query().IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
            return ApiResponse<AccountRequestResponse>.ConflictResponse("An account with this email already exists");

        // Reject if there's already a pending request.
        if (await _unitOfWork.AccountRequests.AnyAsync(r => r.Email == email && r.Status == "pending", ct))
            return ApiResponse<AccountRequestResponse>.ConflictResponse("A request for this email is already pending review");

        var entity = new AccountRequest
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = email,
            Phone = request.Phone,
            Note = request.Note,
            RequestedRoleId = requestedRole?.Id,
            RequestedRoleName = requestedRole?.Name,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
        };

        await _unitOfWork.AccountRequests.AddAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<AccountRequestResponse>.SuccessResponse(
            _mapper.Map<AccountRequestResponse>(entity),
            "Your request has been submitted and is awaiting approval.");
    }

    public async Task<ApiResponse<PaginatedResponse<AccountRequestResponse>>> GetRequestsAsync(PagedRequest request, string status, CancellationToken ct = default)
    {
        var query = _unitOfWork.AccountRequests.QueryNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(r => r.Email.Contains(term) || r.FirstName.Contains(term) || r.LastName.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var mapped = _mapper.Map<List<AccountRequestResponse>>(items);
        var paged = new PaginatedResponse<AccountRequestResponse>(mapped, total, request.PageNumber, request.PageSize);
        return ApiResponse<PaginatedResponse<AccountRequestResponse>>.SuccessResponse(paged);
    }

    public async Task<ApiResponse<AccountRequestResponse>> ApproveAsync(Guid id, ApproveAccountRequest decision, Guid reviewerId, CancellationToken ct = default)
    {
        var req = await _unitOfWork.AccountRequests.GetByIdAsync(id, ct);
        if (req == null)
            return ApiResponse<AccountRequestResponse>.NotFoundResponse("Request not found");
        if (req.Status != "pending")
            return ApiResponse<AccountRequestResponse>.ErrorResponse($"Request is already {req.Status}");

        // Fall back to the role the requester asked for when the admin doesn't override.
        var roleId = decision.RoleId != Guid.Empty ? decision.RoleId : (req.RequestedRoleId ?? Guid.Empty);
        if (roleId == Guid.Empty || !await _unitOfWork.Roles.AnyAsync(r => r.Id == roleId, ct))
            return ApiResponse<AccountRequestResponse>.ErrorResponse("A valid roleId is required to approve");

        // Guard against a duplicate user created between request and approval.
        if (await _unitOfWork.Users.Query().IgnoreQueryFilters().AnyAsync(u => u.Email == req.Email, ct))
            return ApiResponse<AccountRequestResponse>.ConflictResponse("A user with this email already exists");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = req.Email,
            UserName = req.Email,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Phone = req.Phone,
            PasswordHash = req.PasswordHash,
            IsActive = true,
            RoleId = roleId,
        };
        user.SetCreationAudit(reviewerId);
        await _unitOfWork.Users.AddAsync(user, ct);

        req.Status = "approved";
        req.ReviewedBy = reviewerId;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewNote = decision.ReviewNote;
        req.CreatedUserId = user.Id;
        req.SetUpdateAudit(reviewerId);
        _unitOfWork.AccountRequests.Update(req);

        await _unitOfWork.SaveChangesAsync(ct);

        // Fire-and-forget: email failure must not roll back the approval.
        _ = Task.Run(async () =>
        {
            try { await _emailService.SendAccountApprovedAsync(req.Email, req.FirstName, LoginUrl); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not send approval email to {Email}", req.Email); }
        });

        return ApiResponse<AccountRequestResponse>.SuccessResponse(
            _mapper.Map<AccountRequestResponse>(req), "Account approved");
    }

    public async Task<ApiResponse<AccountRequestResponse>> RejectAsync(Guid id, RejectAccountRequest decision, Guid reviewerId, CancellationToken ct = default)
    {
        var req = await _unitOfWork.AccountRequests.GetByIdAsync(id, ct);
        if (req == null)
            return ApiResponse<AccountRequestResponse>.NotFoundResponse("Request not found");
        if (req.Status != "pending")
            return ApiResponse<AccountRequestResponse>.ErrorResponse($"Request is already {req.Status}");

        req.Status = "rejected";
        req.ReviewedBy = reviewerId;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewNote = decision.ReviewNote;
        req.SetUpdateAudit(reviewerId);
        _unitOfWork.AccountRequests.Update(req);

        await _unitOfWork.SaveChangesAsync(ct);

        _ = Task.Run(async () =>
        {
            try { await _emailService.SendAccountRejectedAsync(req.Email, req.FirstName, req.ReviewNote); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not send rejection email to {Email}", req.Email); }
        });

        return ApiResponse<AccountRequestResponse>.SuccessResponse(
            _mapper.Map<AccountRequestResponse>(req), "Request rejected");
    }
}
