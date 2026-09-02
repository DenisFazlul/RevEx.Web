namespace RevEx.Domain.Entities;

public sealed class Category : AuditableEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ICollection<Content> Contents { get; set; } = [];
}
