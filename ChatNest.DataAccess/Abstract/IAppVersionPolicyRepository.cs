using ChatNest.Entities.Models;

namespace ChatNest.DataAccess.Abstract;

public interface IAppVersionPolicyRepository
{
    Task<AppVersionPolicy?> FindAsync(string platform, string channel, CancellationToken cancellationToken = default);
}
