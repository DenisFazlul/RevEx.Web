using System.ComponentModel.DataAnnotations;
using RevEx.Domain.Entities;

namespace RevEx.Api.Models;

public sealed class VersionImportForm
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

    [Required, EnumDataType(typeof(Platform))]
    public Platform? Platform { get; init; }

    [Required, MaxLength(100)]
    public string Application { get; init; } = string.Empty;

    [Required]
    public string ApplicationVersion { get; init; } = string.Empty;

    [Required]
    public IFormFile File { get; init; } = null!;

    [Required]
    public string MetadataJson { get; init; } = string.Empty;

    public List<IFormFile> AuxiliaryFiles { get; init; } = [];

    public Dictionary<int, string> AuxiliaryMetadataJson { get; init; } = [];
}
