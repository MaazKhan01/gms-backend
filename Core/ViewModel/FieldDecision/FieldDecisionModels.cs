using System;

namespace Core.ViewModel.FieldDecision;

/// <summary>One decision taken on the ground and written into the mission file.</summary>
public class FieldDecisionResponse
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string DecisionNote { get; set; }
    public string DecidedByName { get; set; }
    /// <summary>When the decision was taken, which is not necessarily when it
    /// was typed in.</summary>
    public DateTime DecidedAt { get; set; }
}

public class CreateFieldDecisionRequest
{
    public Guid EventId { get; set; }
    public string DecisionNote { get; set; }
    /// <summary>Optional — defaults to now. Set it when logging a decision after
    /// the fact, which is the usual case.</summary>
    public DateTime? DecidedAt { get; set; }
}

public class UpdateFieldDecisionRequest
{
    public Guid Id { get; set; }
    public string DecisionNote { get; set; }
    public DateTime? DecidedAt { get; set; }
}
