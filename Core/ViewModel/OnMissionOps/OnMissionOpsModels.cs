using System;
using System.Collections.Generic;

namespace Core.ViewModel.OnMissionOps;

/// <summary>One broadcast the coordinator sent to the delegation on the ground.</summary>
public class GatheringNotificationResponse
{
    public Guid Id { get; set; }
    public string Message { get; set; }
    /// <summary>Null means it went to the whole delegation.</summary>
    public string Subgroup { get; set; }
    /// <summary>Counted at send time — the roster may have changed since.</summary>
    public int RecipientCount { get; set; }
    public string SentByName { get; set; }
    public DateTime SentAt { get; set; }
}

public class SendGatheringNotificationRequest
{
    public Guid EventId { get; set; }
    public string Message { get; set; }
    /// <summary>Omit to reach the whole delegation.</summary>
    public string Subgroup { get; set; }
}

/// <summary>A subgroup and how many delegates are in it — drives both the target
/// picker and the headcount panel.</summary>
public class SubgroupHeadcountResponse
{
    /// <summary>Null for delegates with no subgroup assigned.</summary>
    public string Subgroup { get; set; }
    public int Headcount { get; set; }
}
