using System.ComponentModel.DataAnnotations;

namespace RevEx.Application.DTOs.ContentVersion;

public sealed class UpdateContentVersionDto
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    public DateTimeOffset Date { get; init; }

    [Required, RegularExpression("^(draft|published|archived)$")]
    public string Status { get; init; } = string.Empty;
}
