namespace RevEx.Infrastructure.Persistence;
public sealed class ContentSubscription
{
public long Id {get;set;} public required string UserId {get;set;} public int ContentId {get;set;} public required string Email {get;set;} public DateTimeOffset SubscribedAt {get;set;}
}
