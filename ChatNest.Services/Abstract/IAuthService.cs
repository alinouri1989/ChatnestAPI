using ChatNest.Shared.DTOs.Request;
using ChatNest.Shared.DTOs.Response;
using Microsoft.AspNetCore.Identity;

namespace ChatNest.Services.Abstract
{
    public interface IAuthService
    {
        Task<IdentityResult> SignUpAsync(SignUp dto);
        Task<AuthTokenResponse> SignInEmailAsync(SignInEmail dto);
        Task<AuthTokenResponse> SignInGoogleAsync(SignInProvider dto);
        Task<AuthTokenResponse> SignInFacebookAsync(SignInProvider dto);
        Task RequestLoginOtpAsync(RequestLoginOtp dto);
        Task<AuthTokenResponse> VerifyLoginOtpAsync(VerifyLoginOtp dto);
        Task<AuthTokenResponse> RefreshTokenAsync(string refreshToken);
        Task RevokeRefreshTokenAsync(string refreshToken);
        Task ResetPasswordAsync(string email);
        Task ConfirmResetPasswordAsync(ResetPasswordConfirm dto);
        Task<PasswordSecurityQuestionPrompt> GetPasswordFallbackQuestionAsync(string email);
        Task ResetPasswordByIdentityAsync(ResetPasswordFallback dto);
    }
}
