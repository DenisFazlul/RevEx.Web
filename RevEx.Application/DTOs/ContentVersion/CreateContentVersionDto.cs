using System.ComponentModel.DataAnnotations;

namespace RevEx.Application.DTOs.ContentVersion;

public sealed class CreateContentVersionDto
{
    [Range(1, int.MaxValue)]
    public int ContentId { get; init; }

    [MaxLength(200)]
    public string? Name { get; init; }

    public DateTimeOffset? Date { get; init; }

    [RegularExpression("^(draft|published|archived)$")]
    public string? Status { get; init; }
}
