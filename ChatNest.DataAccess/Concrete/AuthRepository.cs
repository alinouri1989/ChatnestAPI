using ChatNest.DataAccess.Abstract;
using ChatNest.Entities.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ChatNest.DataAccess.Contexts;
using ChatNest.Entities.Identity;

namespace ChatNest.DataAccess.Concrete
{
    public sealed class AuthRepository : IAuthRepository
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ChatNestDbContext _dbContext;

        public AuthRepository(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ChatNestDbContext dbContext)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _dbContext = dbContext;
        }

        public async Task<IdentityResult> CreateUserAsync(User user, string password)
        {
            return await _userManager.CreateAsync(user, password);
        }

        public async Task<SignInResult> SignInWithEmailAsync(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return SignInResult.Failed;

            return await _signInManager.PasswordSignInAsync(user, password, false, false);
        }

        public async Task<IdentityResult> ChangePasswordAsync(User user, string currentPassword, string newPassword)
        {
            return await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        }

        public async Task<IdentityResult> ResetPasswordAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return IdentityResult.Failed();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            // Here you would typically send an email with the reset link
            // For now, we'll just return success
            return IdentityResult.Success;
        }

        public async Task<User?> FindByEmailAsync(string email)
        {
            return await _userManager.FindByEmailAsync(email);
        }

        public async Task<User?> FindByIdAsync(string userId)
        {
            return await _userManager.FindByIdAsync(userId);
        }

        public async Task AddRefreshTokenAsync(RefreshToken refreshToken)
        {
            _dbContext.Set<RefreshToken>().Add(refreshToken);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<string?> RotateRefreshTokenAsync(
            string currentTokenHash,
            RefreshToken replacementToken)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;

            var currentToken = await _dbContext.Set<RefreshToken>()
                .AsNoTracking()
                .Where(token => token.Token == currentTokenHash &&
                                token.IsActive &&
                                token.Revoked == null &&
                                token.Expiration > now)
                .Select(token => new { token.UserId })
                .SingleOrDefaultAsync();

            if (currentToken == null)
            {
                return null;
            }

            var updatedRows = await _dbContext.Set<RefreshToken>()
                .Where(token => token.Token == currentTokenHash &&
                                token.IsActive &&
                                token.Revoked == null &&
                                token.Expiration > now)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(token => token.IsActive, false)
                    .SetProperty(token => token.Revoked, now));

            if (updatedRows != 1)
            {
                await transaction.RollbackAsync();
                return null;
            }

            replacementToken.UserId = currentToken.UserId;
            _dbContext.Set<RefreshToken>().Add(replacementToken);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return currentToken.UserId;
        }

        public async Task RevokeRefreshTokenAsync(string tokenHash)
        {
            var now = DateTime.UtcNow;
            await _dbContext.Set<RefreshToken>()
                .Where(token => token.Token == tokenHash && token.IsActive)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(token => token.IsActive, false)
                    .SetProperty(token => token.Revoked, now));
        }

        public async Task SignOutAsync()
        {
            await _signInManager.SignOutAsync();
        }
    }
}
