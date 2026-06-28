using ChatNest.DataAccess.Abstract;
using ChatNest.DataAccess.Contexts;
using ChatNest.Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatNest.DataAccess.Concrete
{
    public sealed class UserRepository : IUserRepository
    {
        private readonly ChatNestDbContext _context;

        public UserRepository(ChatNestDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }

        public async Task CreateUserAsync(User user)
        {
            user.CreatedDate = DateTime.UtcNow;
            user.LastConnectionDate = DateTime.UtcNow;

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        public async Task<User?> GetUserByIdAsync(string userId)
        {
            return await _context.Users.FindAsync(userId);
        }

        public async Task<Dictionary<string, User>> GetUsersByIdsAsync(IEnumerable<string> userIds)
        {
            var uniqueIds = (userIds ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (uniqueIds.Count == 0)
            {
                return new Dictionary<string, User>();
            }

            var users = await _context.Users
                .Where(u => uniqueIds.Contains(u.Id))
                .ToListAsync();

            return users.ToDictionary(u => u.Id, u => u);
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User?> GetUserByProviderIdAsync(string providerId)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.ProviderId == providerId);
        }

        public async Task UpdateUserFieldAsync(string userId, string fieldName, object newValue)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return;

            var property = typeof(User).GetProperty(fieldName);
            if (property != null && property.CanWrite)
            {
                property.SetValue(user, newValue);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateUserAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateLastConnectionDateAsync(string userId, DateTime lastConnectionDate)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.LastConnectionDate = lastConnectionDate;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Dictionary<string, List<string>>> GetFcmTokensByUserIdsAsync(IEnumerable<string> userIds)
        {
            var usersById = await GetUsersByIdsAsync(userIds);
            return usersById.ToDictionary(
                item => item.Key,
                item => item.Value.FcmTokens
                    .Where(token => !string.IsNullOrWhiteSpace(token))
                    .Distinct()
                    .ToList());
        }

        public async Task RemoveFcmTokensAsync(IEnumerable<string> tokens)
        {
            var tokensToRemove = (tokens ?? Enumerable.Empty<string>())
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct()
                .ToHashSet(StringComparer.Ordinal);

            if (tokensToRemove.Count == 0)
                return;

            var users = await _context.Users
                .Where(user => !string.IsNullOrWhiteSpace(user.FcmTokensJson))
                .ToListAsync();

            var changed = false;
            foreach (var user in users)
            {
                var currentTokens = user.FcmTokens;
                var filteredTokens = currentTokens
                    .Where(token => !tokensToRemove.Contains(token))
                    .ToList();

                if (filteredTokens.Count != currentTokens.Count)
                {
                    user.FcmTokens = filteredTokens;
                    changed = true;
                }
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> DeleteUserAsync(string userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<IEnumerable<User>> SearchUsersAsync(string searchTerm)
        {
            return await _context.Users
                .Where(u => u.DisplayName.Contains(searchTerm) ||
                           u.Email.Contains(searchTerm) ||
                           (u.UserIdentifier != null && u.UserIdentifier.Contains(searchTerm)))
                .ToListAsync();
        }
    }
}
