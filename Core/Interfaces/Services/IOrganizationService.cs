using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.Organization;

namespace Core.Interfaces.Services;

public interface IOrganizationService
{
    Task<ApiResponse<List<OrganizationResponse>>> GetAllAsync(CancellationToken ct = default);
    Task<ApiResponse<OrganizationResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<OrganizationResponse>> CreateAsync(CreateOrganizationRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<OrganizationResponse>> UpdateAsync(Guid id, UpdateOrganizationRequest request, int userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAsync(Guid id, int userId, CancellationToken ct = default);
}
