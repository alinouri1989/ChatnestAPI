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
