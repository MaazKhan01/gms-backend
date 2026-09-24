using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.OnMissionOps;

namespace Core.Interfaces.Services;

public interface IOnMissionOpsService
{
    /// <summary>Subgroups on a mission with their headcount. Drives the target
    /// picker and the headcount panel.</summary>
    Task<ApiResponse<List<SubgroupHeadcountResponse>>> GetSubgroupsAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>What the coordinator has sent, newest first.</summary>
    Task<ApiResponse<List<GatheringNotificationResponse>>> GetNotificationsAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Fans the message out to the delegation (or one subgroup) and
    /// records it in the outbox.</summary>
    Task<ApiResponse<GatheringNotificationResponse>> SendNotificationAsync(
        SendGatheringNotificationRequest request, int userId, CancellationToken ct = default);
}
