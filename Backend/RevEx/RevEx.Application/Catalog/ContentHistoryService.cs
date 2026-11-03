using System.Security.Claims;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.ContentHistory;
using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Application.Catalog;
using RevEx.Application.Content;

public sealed class ContentHistoryService(RevEx.Application.Abstractions.Identity.ICurrentUser currentUser,

    IContentRepository contentRepository,
    IContentHistoryStore historyStore)
{

    public async Task<IReadOnlyCollection<ContentHistoryEventDto>> Get(
        int contentId,
        CancellationToken cancellationToken)
    {
        var events = await historyStore.GetByContentIdAsync(contentId, cancellationToken);
        return events;
    }

    public async Task<ContentHistoryEventDto> Create(
        int contentId,
        CreateContentHistoryEventDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ContentOperationException(ContentFailure.Validation, "Description must not be empty.");
        }

        if (await contentRepository.GetByIdAsync(contentId, cancellationToken) is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound);
        }

        var description = request.Description.Trim();
        if (description.Length > 2000)
        {
            throw new ContentOperationException(ContentFailure.Validation, "Description must not exceed 2000 characters.");
        }

        var created = await historyStore.AddAsync(
            contentId,
            description,
            currentUser.Author,
            cancellationToken);
        return created;
    }

}
