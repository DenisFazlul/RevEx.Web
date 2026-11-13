using System.ComponentModel.DataAnnotations;

namespace RevEx.Api.Models;

public sealed class RfaImportForm
{
    [Required]
    public Guid Cid { get; init; }

    [Required]
    public Guid Vid { get; init; }

    [Required]
    public string ContentName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int? CategoryId { get; init; }

    [Required]
    public string VersionName { get; init; } = string.Empty;

    public DateTimeOffset Date { get; init; }

    [Required]
    public string ApplicationVersion { get; init; } = string.Empty;

    [Required]
    public IFormFile File { get; init; } = null!;

    [Required]
    public string MetadataJson { get; init; } = string.Empty;

    public IReadOnlyCollection<IFormFile> AuxiliaryFiles { get; init; } = [];
}
