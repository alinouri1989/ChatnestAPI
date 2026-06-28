using ChatNest.Entities.Models;

namespace ChatNest.DataAccess.Abstract
{
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task CreateUserAsync(User user);
        Task<User?> GetUserByIdAsync(string userId);
        Task<Dictionary<string, User>> GetUsersByIdsAsync(IEnumerable<string> userIds);
        Task<User?> GetUserByEmailAsync(string email);
        Task<User?> GetUserByProviderIdAsync(string providerId);
        Task UpdateUserFieldAsync(string userId, string fieldName, object newValue);
        Task UpdateUserAsync(User user);
        Task UpdateLastConnectionDateAsync(string userId, DateTime lastConnectionDate);
        Task<Dictionary<string, List<string>>> GetFcmTokensByUserIdsAsync(IEnumerable<string> userIds);
        Task RemoveFcmTokensAsync(IEnumerable<string> tokens);
        Task<bool> DeleteUserAsync(string userId);
        Task<IEnumerable<User>> SearchUsersAsync(string searchTerm);
    }
}
