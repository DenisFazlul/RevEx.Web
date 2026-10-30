namespace RevEx.Application.Subscriptions;

public interface IContentSubscriptions
{
    Task<bool> GetAsync(int contentId, string userId, CancellationToken token);
    Task SetAsync(int contentId, string userId, string? email, bool enabled, CancellationToken token);
}

