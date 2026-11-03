using RevEx.Application.Subscriptions;
sealed class Subscriptions : IContentSubscriptions
{
 public Task<bool> GetAsync(int contentId, string userId, CancellationToken token) => Task.FromResult(false);
 public Task SetAsync(int contentId, string userId, string? email, bool enabled, CancellationToken token) => Task.CompletedTask;
}
