using System;

namespace DomainPersistence.Entities;

/// <summary>
/// An on-the-ground decision taken by the Head of Delegation and recorded for the
/// mission file. Deliberately thin: this is a log, not a workflow — there is no
/// approval, no status and no reversal, because the decision was already taken in
/// the field before anyone typed it in.
/// </summary>
public class FieldDecision : Entity
{
    public int EventId { get; set; }

    public string DecisionNote { get; set; }

    public int? DecidedBy { get; set; }
    public DateTime DecidedAt { get; set; }

    public virtual Event Event { get; set; }
    public virtual User DecidedByUser { get; set; }
}
