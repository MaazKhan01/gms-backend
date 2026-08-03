using System;
using System.Collections.Generic;

namespace Core.ViewModel.Common;

public class ImportBatchRowDto
{
    public int Row { get; set; }
    public string Title { get; set; }
    public bool Success { get; set; }
    public string Error { get; set; }
    public string ErrorCategory { get; set; }
}

// Polled by the frontend after a bulk import (Events/Guests) is kicked off —
// "queued" | "processing" | "completed" | "failed". Rows accumulate as the
// background job processes them, so the UI can show live progress.
public class ImportBatchStatusDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; }
    public string Status { get; set; }
    public int Total { get; set; }
    public int Imported { get; set; }
    public int Failed { get; set; }
    public string ErrorMessage { get; set; }
    public List<ImportBatchRowDto> Rows { get; set; } = new();
}

public class StartImportResponse
{
    public Guid BatchId { get; set; }
    public string Status { get; set; }
}
