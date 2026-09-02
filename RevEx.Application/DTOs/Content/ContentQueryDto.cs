namespace RevEx.Application.DTOs.Content;

public sealed class ContentQueryDto
{
    public int[] CategoryIds { get; init; } = [];
    public string? Name { get; init; }
}
