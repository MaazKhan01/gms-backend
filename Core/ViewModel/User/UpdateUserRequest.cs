using System;

namespace Core.ViewModel.User;

public class UpdateUserRequest
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Phone { get; set; }
    public Guid? RoleId { get; set; }
    public bool? IsActive { get; set; }
}
