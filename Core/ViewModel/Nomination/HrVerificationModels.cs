using System;
using System.Collections.Generic;

namespace Core.ViewModel.Nomination;

/// <summary>Result of a bulk verify or reject. Partial success is normal —
/// a row someone else already handled is skipped, not fatal.</summary>
public class HrVerificationResult
{
    public int Updated { get; set; }
    public int Skipped { get; set; }
    /// <summary>Why each skipped row was skipped, keyed by participation id.</summary>
    public List<HrVerificationSkip> Skips { get; set; } = new();
}

public class HrVerificationSkip
{
    public Guid Id { get; set; }
    public string Reason { get; set; }
}

public class HrVerifyRequest
{
    /// <summary>Participation ids to verify. One id is just a bulk of one.</summary>
    public List<Guid> Ids { get; set; } = new();
    public string Note { get; set; }
}

public class HrRejectRequest
{
    public List<Guid> Ids { get; set; } = new();
    /// <summary>Required — a rejection with no reason is not actionable by the
    /// coordinator who has to fix it.</summary>
    public string Note { get; set; }
}
