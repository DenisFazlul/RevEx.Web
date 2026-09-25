namespace RevEx.Domain.Entities;

public sealed class Content : AuditableEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int CategoryId { get; set; }
    public int ContentStatusId { get; set; }
    public int? PreviewFileId { get; set; }
    public ICollection<int> TagIds { get; set; } = [];
    public Category Category { get; set; } = null!;
    public ContentStatus ContentStatus { get; set; } = null!;
    public ContentFile? PreviewFile { get; set; }
    public ICollection<ContentVersion> Versions { get; set; } = [];
    public ICollection<Tag> Tags { get; set; } = [];
}
