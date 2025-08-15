using ChatNest.Entities.Models;
using Microsoft.AspNetCore.Identity;

namespace ChatNest.DataAccess.Abstract
{
    public interface IAuthRepository
    {
        Task<IdentityResult> CreateUserAsync(User user, string password);
        Task<SignInResult> SignInWithEmailAsync(string email, string password);
        Task<IdentityResult> ChangePasswordAsync(User user, string currentPassword, string newPassword);
        Task<IdentityResult> ResetPasswordAsync(string email);
        Task<User?> FindByEmailAsync(string email);
        Task<User?> FindByIdAsync(string userId);
        Task SignOutAsync();
    }
}