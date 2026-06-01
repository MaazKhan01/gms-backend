namespace Core.ViewModel.Auth;

public class VerifyOtpRequest
{
    public string Email { get; set; }
    public string OtpCode { get; set; }
}
