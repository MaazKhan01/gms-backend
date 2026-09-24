using System;

namespace DomainPersistence.Entities;

/// <summary>
/// The mission's single consolidated report: assembled from the delegates'
/// individual reports, edited by the Head of Delegation, then approved and
/// published by Protocol. One per mission.
///
/// Two distinct sign-offs are kept apart on purpose — reviewing (HoD) and
/// approving (Protocol) are different people answering different questions.
/// </summary>
public class CombinedReport : Entity
{
    public int EventId { get; set; }

    /// <summary>Core.Constants.CombinedReportStatuses — draft / in_review /
    /// approved / published.</summary>
    public string Status { get; set; }

    /// <summary>The assembled body, editable before approval.</summary>
    public string ContentHtml { get; set; }

    public DateTime? AssembledOn { get; set; }

    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedOn { get; set; }

    public int? ApprovedBy { get; set; }
    public DateTime? ApprovedOn { get; set; }

    /// <summary>Final archived PDF, as a blob URL. Set on publish.</summary>
    public string PublishedUrl { get; set; }

    public virtual Event Event { get; set; }
    public virtual User ReviewedByUser { get; set; }
    public virtual User ApprovedByUser { get; set; }
}
