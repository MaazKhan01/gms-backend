using System.Collections.Generic;

namespace Core.ViewModel.Event;

public class ImportEventRowResult
{
    public int Row { get; set; }
    public string Title { get; set; }
    public bool Success { get; set; }
    public string Error { get; set; }
    // "stale_venue" | "stale_type" | "validation" | null — lets the frontend
    // single out rows that failed because the template is outdated (a Venue/
    // Type value that no longer/never existed) from ordinary validation
    // failures, and prompt to re-export a fresh template for just those.
    public string ErrorCategory { get; set; }
}

public class ImportEventsResult
{
    public int Total { get; set; }
    public int Imported { get; set; }
    public int Failed { get; set; }
    public List<ImportEventRowResult> Rows { get; set; } = new();
}
