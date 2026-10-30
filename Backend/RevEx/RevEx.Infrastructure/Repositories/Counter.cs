using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;
using ContentEntity = RevEx.Domain.Entities.Content;
using DomainTag = RevEx.Domain.Entities.Tag;

namespace RevEx.Infrastructure.Repositories;

internal sealed class Counter
    {
        [BsonId] public required string Id { get; init; }
        public int Sequence { get; init; }
    }
