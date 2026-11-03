using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RevEx.Application.Subscriptions;
using RevEx.Infrastructure.Persistence;
namespace RevEx.Infrastructure.Notifications;
public sealed class ContentSubscriptions(IOptions<ContentHistoryOptions> options) : IContentSubscriptions
{
    private readonly PostgresContextFactory factory = new(options);
    public async Task<bool> GetAsync(int contentId, string userId, CancellationToken token)
    {
        await using var db = factory.Create();
        return await db.Database.SqlQuery<bool>($"SELECT EXISTS(SELECT 1 FROM content_subscriptions WHERE content_id={contentId} AND user_id={userId}) AS \"Value\"").SingleAsync(token);
    }
    public async Task SetAsync(int contentId, string userId, string? email, bool enabled, CancellationToken token)
    {
        await using var db = factory.Create();
        if (enabled)
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO content_subscriptions(content_id,user_id,email) VALUES({contentId},{userId},{email}) ON CONFLICT(content_id,user_id) DO UPDATE SET email=EXCLUDED.email", token);
        else await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM content_subscriptions WHERE content_id={contentId} AND user_id={userId}", token);
    }
}
