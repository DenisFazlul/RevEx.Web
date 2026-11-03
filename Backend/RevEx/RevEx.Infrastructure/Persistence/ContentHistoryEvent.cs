namespace RevEx.Infrastructure.Persistence;
public sealed class ContentHistoryEvent
{
    public long Id { get; set; }
    public int ContentId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public required string Description { get; set; }
    public required string Author { get; set; }
}
