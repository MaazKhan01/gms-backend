using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Auth;
using Core.ViewModel.Common;
using Core.ViewModel.User;

namespace Core.Interfaces.Services;

public interface IAuthService
{
    /// <param name="clientApp">The X-Client-App header value ("portal" / "driver-app").
    /// Null/unknown is treated as the portal.</param>
    Task<ApiResponse<TokenResponse>> LoginAsync(LoginModel model, string clientApp = null, CancellationToken ct = default);
    Task<ApiResponse<TokenResponse>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<ApiResponse<bool>> RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<ApiResponse<bool>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);
    Task<ApiResponse<UserResponse>> ValidateResetPasswordTokenAsync(ValidateToken token, CancellationToken ct = default);
    Task<ApiResponse<TokenResponse>> ValidateToken(string token, CancellationToken ct = default);
    Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
    Task<ApiResponse<TokenResponse>> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default);
}
