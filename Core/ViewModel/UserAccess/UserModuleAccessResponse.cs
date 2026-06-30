using System;
using System.Collections.Generic;

namespace Core.ViewModel.UserAccess;

public class ModuleAccessItem
{
    public string Slug { get; set; }
    public string DisplayName { get; set; }
    public string ViewPermission { get; set; }
    /// <summary>True when the user's role already includes this module natively.</summary>
    public bool IsNative { get; set; }
    /// <summary>True when admin has explicitly granted extra access.</summary>
    public bool IsGranted { get; set; }
}

public class UserModuleAccessResponse
{
    public Guid UserId { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string RoleName { get; set; }
    public List<ModuleAccessItem> Modules { get; set; } = new();
}
