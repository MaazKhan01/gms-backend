namespace Core.ViewModel.Common;

public class PagedRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SearchTerm { get; set; }
    public string SortBy { get; set; }
    public bool SortDescending { get; set; }
    // When true, guests who declined their invitation are omitted (used by the
    // seating/meeting/travel pickers so a rejected guest can't be assigned).
    public bool ExcludeDeclined { get; set; }
}
 public class NotificationPagedRequest : PagedRequest
{
    public bool IsRead { get; set; }
}