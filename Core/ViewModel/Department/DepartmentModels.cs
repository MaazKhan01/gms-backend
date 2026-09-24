using System;

namespace Core.ViewModel.Department;

public class DepartmentResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }
    /// <summary>People currently assigned. Drives the "in use" warning on delete.</summary>
    public int MemberCount { get; set; }
}

public class CreateDepartmentRequest
{
    public string Name { get; set; }
    public string NameAr { get; set; }
}

public class UpdateDepartmentRequest
{
    public string Name { get; set; }
    public string NameAr { get; set; }
}
