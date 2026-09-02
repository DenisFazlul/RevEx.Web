namespace RevEx.Domain.Entities;

public sealed class ContentFile : AuditableEntity
{
    public int Id { get; set; }
    public required string Url { get; set; }
    public int? ContentVersionId { get; set; }
    public ContentVersion? ContentVersion { get; set; }
}
