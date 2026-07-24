using System;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.UserAccess;

namespace Core.Interfaces.Services;

public interface IUserAccessService
{
    Task<ApiResponse<UserModuleAccessResponse>> GetUserModuleAccessAsync(Guid userId, CancellationToken ct = default);
    Task<ApiResponse<UserModuleAccessResponse>> SetUserModuleAccessAsync(Guid userId, SetModuleAccessRequest request, int adminId, CancellationToken ct = default);
}
