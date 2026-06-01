using System;

namespace Core.Common;

public class LoggedInUser
{
    public Guid Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public Guid RoleId { get; set; }
    public string RoleName { get; set; }
    public string Status { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
