using RevEx.Domain.Contracts.Content;

namespace RevEx.Api.Models;

public sealed record ContentPage(
    IReadOnlyCollection<ContentDto> Items,
    int Page,
    int PageSize,
    bool HasNextPage);
