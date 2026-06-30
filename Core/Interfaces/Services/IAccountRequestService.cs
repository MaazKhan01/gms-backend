using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.AccountRequest;
using Core.ViewModel.Common;

namespace Core.Interfaces.Services;

public interface IAccountRequestService
{
    // Public — roles a person may request at sign-up (excludes admin).
    Task<ApiResponse<List<RequestableRoleResponse>>> GetRequestableRolesAsync(CancellationToken ct = default);

    // Public — submit a sign-up request (pending admin approval).
    Task<ApiResponse<AccountRequestResponse>> SubmitAsync(RegisterAccountRequest request, CancellationToken ct = default);

    // Admin — review queue.
    Task<ApiResponse<PaginatedResponse<AccountRequestResponse>>> GetRequestsAsync(PagedRequest request, string status, CancellationToken ct = default);
    Task<ApiResponse<AccountRequestResponse>> ApproveAsync(Guid id, ApproveAccountRequest decision, Guid reviewerId, CancellationToken ct = default);
    Task<ApiResponse<AccountRequestResponse>> RejectAsync(Guid id, RejectAccountRequest decision, Guid reviewerId, CancellationToken ct = default);
}
