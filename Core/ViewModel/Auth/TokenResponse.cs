using System;
using System.Collections.Generic;

namespace Core.ViewModel.Auth;

public class TokenResponse
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public UserInfo User { get; set; }
}

public class UserInfo
{
    public Guid Id { get; set; }
    public string UserName { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FullName => $"{FirstName} {LastName}";
    public string Email { get; set; }
    public string Role { get; set; }
    public Guid? RoleId { get; set; }
    public string RoleCode { get; set; }
    public List<string> Permissions { get; set; }
}
