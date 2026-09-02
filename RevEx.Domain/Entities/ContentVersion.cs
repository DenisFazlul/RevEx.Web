namespace RevEx.Domain.Entities;

public sealed class ContentVersion : AuditableEntity
{
    public int Id { get; set; }
    public int ContentId { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset Date { get; set; }
    public required string Status { get; set; }
    public Content Content { get; set; } = null!;
    public ICollection<ContentFile> Files { get; set; } = [];
}
