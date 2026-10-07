namespace RevEx.Domain.Entities;

public sealed class TagGroup : AuditableEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ICollection<Tag> Tags { get; set; } = [];
}
