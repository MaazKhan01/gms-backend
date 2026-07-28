using System;

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
// Guest list filters. These sit alongside paging because the guest table pages
// server-side — filtering client-side would only ever filter the current page.
public class GuestPagedRequest : PagedRequest
{
    public string Tier { get; set; }
    public string InvitationStatus { get; set; }
}

 public class NotificationPagedRequest : PagedRequest
{
    public bool IsRead { get; set; }

    // Optional filters — notification history support.
    public string Type { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}