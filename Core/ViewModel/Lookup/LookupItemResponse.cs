using System;
using System.Collections.Generic;

namespace Core.ViewModel.Lookup;

public class LookupItemResponse
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryCode { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
