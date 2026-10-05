using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.FieldDecision;

namespace Core.Interfaces.Services;

public interface IFieldDecisionService
{
    /// <summary>The mission's decision log, newest decision first.</summary>
    Task<ApiResponse<List<FieldDecisionResponse>>> GetAsync(Guid eventId, CancellationToken ct = default);

    Task<ApiResponse<FieldDecisionResponse>> CreateAsync(
        CreateFieldDecisionRequest request, int userId, CancellationToken ct = default);

    /// <summary>Corrects the wording or the time. The decision itself is not
    /// reversible — that is what a follow-up entry is for.</summary>
    Task<ApiResponse<FieldDecisionResponse>> UpdateAsync(
        UpdateFieldDecisionRequest request, int userId, CancellationToken ct = default);

    /// <summary>For an entry logged by mistake. Soft delete.</summary>
    Task<ApiResponse<bool>> DeleteAsync(Guid id, int userId, CancellationToken ct = default);
}
