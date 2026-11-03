using System.Net.Mail;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Content;
using ContentEntity = RevEx.Domain.Entities.Content;
namespace RevEx.Application.Subscriptions;
public sealed class ContentSubscriptionService(IContentSubscriptions subscriptions, IContentRepository contents)
{
 public async Task<bool> Get(int id, string userId, CancellationToken token)
 {
  await EnsureContent(id, token);
  return await subscriptions.GetAsync(id, userId, token);
 }
 public async Task Subscribe(int id, string userId, string? email, CancellationToken token)
 {
  await EnsureContent(id, token);
  if (!MailAddress.TryCreate(email, out _)) throw new ContentOperationException(ContentFailure.Validation, "В профиле пользователя отсутствует адрес электронной почты.");
  await subscriptions.SetAsync(id, userId, email, true, token);
 }
 public Task Unsubscribe(int id, string userId, CancellationToken token) => subscriptions.SetAsync(id, userId, null, false, token);
 private async Task EnsureContent(int id, CancellationToken token)
 {
  if (await contents.GetByIdAsync(id, token) is null) throw new ContentOperationException(ContentFailure.NotFound);
 }
}
