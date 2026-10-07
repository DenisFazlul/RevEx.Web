namespace RevEx.Domain.Entities;

public sealed class ContentFile : AuditableEntity
{
    public int Id { get; set; }
    public required string Url { get; set; }
    public required string OriginalFileName { get; set; }
    public required string Extension { get; set; }
    public long SizeBytes { get; set; }
    public required string Role { get; set; }
    public int? ContentVersionId { get; set; }
    public ContentVersion? ContentVersion { get; set; }
}
