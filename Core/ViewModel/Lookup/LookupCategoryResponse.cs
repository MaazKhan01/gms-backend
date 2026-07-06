using System;

namespace Core.ViewModel.Lookup;

public class LookupCategoryResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystem { get; set; }
    public int ItemCount { get; set; }
}
