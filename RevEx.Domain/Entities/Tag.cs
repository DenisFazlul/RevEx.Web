namespace RevEx.Domain.Entities;

public sealed class Tag : AuditableEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int TagGroupId { get; set; }
    public TagGroup? TagGroup { get; set; }
    public ICollection<Content> Contents { get; set; } = [];
}
