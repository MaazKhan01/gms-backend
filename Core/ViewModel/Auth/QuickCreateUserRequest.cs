namespace Core.ViewModel.Auth;

public class QuickCreateUserRequest
{
    public string Email { get; set; }
    // Full name — first token is the first name, the remainder the last name.
    public string Name { get; set; }
    // Role Code or Name, e.g. "Admin".
    public string Role { get; set; }
    public string Password { get; set; }
}
