using System;
using System.Collections.Generic;

namespace Core.ViewModel.Lookup;

/// <summary>
/// Used for both create and update. On update, <see cref="Id"/> is set from the route.
/// The category is addressed by its stable <see cref="CategoryCode"/> (e.g. "AIRPORT").
/// </summary>
public class LookupItemRequest
{
    public Guid? Id { get; set; }
    public string CategoryCode { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public Dictionary<string, string> Metadata { get; set; }
}
