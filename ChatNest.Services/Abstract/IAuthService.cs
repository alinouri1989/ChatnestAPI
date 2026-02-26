using ChatNest.Shared.DTOs.Request;
using ChatNest.Shared.DTOs.Response;
using Microsoft.AspNetCore.Identity;

namespace ChatNest.Services.Abstract
{
    public interface IAuthService
    {
        Task<IdentityResult> SignUpAsync(SignUp dto);
        Task<string> SignInEmailAsync(SignInEmail dto);
        Task<string> SignInGoogleAsync(SignInProvider dto);
        Task<string> SignInFacebookAsync(SignInProvider dto);
        Task ResetPasswordAsync(string email);
        Task ConfirmResetPasswordAsync(ResetPasswordConfirm dto);
        Task<PasswordSecurityQuestionPrompt> GetPasswordFallbackQuestionAsync(string email);
        Task ResetPasswordByIdentityAsync(ResetPasswordFallback dto);
    }
}
