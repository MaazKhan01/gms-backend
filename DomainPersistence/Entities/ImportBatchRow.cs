namespace DomainPersistence.Entities;

/// <summary>Per-row outcome of one <see cref="ImportBatch"/> — one event/guest per row.</summary>
public class ImportBatchRow : Entity
{
    public int ImportBatchId { get; set; }
    public int RowNumber { get; set; }
    public string Title { get; set; }
    public bool Success { get; set; }
    public string Error { get; set; }
    public string ErrorCategory { get; set; }

    public virtual ImportBatch ImportBatch { get; set; }
}
