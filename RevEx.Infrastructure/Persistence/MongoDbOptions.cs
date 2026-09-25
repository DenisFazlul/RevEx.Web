namespace RevEx.Infrastructure.Persistence;

public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";
    public required string ConnectionString { get; init; }
    public required string DatabaseName { get; init; }
}
