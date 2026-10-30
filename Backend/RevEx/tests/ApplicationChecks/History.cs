using RevEx.Application.Content;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.Abstractions.Identity;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Domain.Entities;

sealed class History : IContentHistoryStore
{
    public List<ContentHistoryEventDto> Items = [];
    public Task InitializeAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task<IReadOnlyCollection<ContentHistoryEventDto>> GetByContentIdAsync(int id, CancellationToken token = default) => Task.FromResult<IReadOnlyCollection<ContentHistoryEventDto>>(Items.Where(x => x.ContentId == id).ToArray());
    public Task<ContentHistoryEventDto> AddAsync(int id, string description, string author, CancellationToken token = default)
    { var item = new ContentHistoryEventDto(Items.Count + 1, id, DateTimeOffset.UtcNow, description, author); Items.Add(item); return Task.FromResult(item); }
}
