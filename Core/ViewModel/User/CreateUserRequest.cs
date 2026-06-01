using System;

namespace Core.ViewModel.User;

public class CreateUserRequest
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string? Phone { get; set; }
    public string? Password { get; set; }
    public Guid RoleId { get; set; }
}
