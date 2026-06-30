using System;

namespace Core.ViewModel.AccountRequest;

// Public sign-up payload (POST /api/v1/auth/register).
public class RegisterAccountRequest
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string Password { get; set; }
    public string Note { get; set; }
    public Guid? RequestedRoleId { get; set; }   // the account type the user is requesting
}

// Public, anonymous — the roles a person may request at sign-up (excludes admin).
public class RequestableRoleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}

public class AccountRequestResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string Note { get; set; }
    public Guid? RequestedRoleId { get; set; }
    public string RequestedRoleName { get; set; }
    public string Status { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string ReviewNote { get; set; }
    public Guid? CreatedUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Admin decision payloads.
public class ApproveAccountRequest
{
    public Guid RoleId { get; set; }      // role to assign to the new user
    public string ReviewNote { get; set; }
}

public class RejectAccountRequest
{
    public string ReviewNote { get; set; }
}
