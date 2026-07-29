using System;
using System.Collections.Generic;

namespace Core.ViewModel.User;

public class UserResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string Email { get; set; }
    public string Phone { get; set; }
    public string Role { get; set; }
    public string RoleName { get; set; }
    /// <summary>The role's public id — what the edit form posts back.</summary>
    public Guid? RoleId { get; set; }
    public bool IsActive { get; set; }
    public List<string> Permissions { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
