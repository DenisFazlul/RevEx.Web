namespace RevEx.Infrastructure.Persistence;

public sealed class ContentHistoryOptions
{
    public const string SectionName = "ContentHistory";

    public string ConnectionString { get; init; } = string.Empty;
}
