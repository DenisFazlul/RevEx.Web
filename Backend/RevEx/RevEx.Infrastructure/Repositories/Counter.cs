using MongoDB.Bson.Serialization.Attributes;
namespace RevEx.Infrastructure.Repositories;
internal sealed class Counter
{
    [BsonId] public required string Id { get; init; }
    public int Sequence { get; init; }
}
