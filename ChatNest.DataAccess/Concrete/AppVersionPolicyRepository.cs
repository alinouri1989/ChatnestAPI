using ChatNest.DataAccess.Abstract;
using ChatNest.DataAccess.Contexts;
using ChatNest.Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatNest.DataAccess.Concrete;

public sealed class AppVersionPolicyRepository(ChatNestDbContext db) : IAppVersionPolicyRepository
{
    public Task<AppVersionPolicy?> FindAsync(
        string platform,
        string channel,
        CancellationToken cancellationToken = default) =>
        db.AppVersionPolicies
            .AsNoTracking()
            .Include(policy => policy.ReleaseNotes.OrderBy(note => note.DisplayOrder))
            .SingleOrDefaultAsync(
                policy => policy.Platform == platform && policy.Channel == channel,
                cancellationToken);
}
