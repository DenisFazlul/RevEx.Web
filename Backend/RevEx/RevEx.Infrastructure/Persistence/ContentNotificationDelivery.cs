namespace RevEx.Infrastructure.Persistence;
public sealed class ContentNotificationDelivery
{
public long EventId {get;set;} public required string UserId {get;set;} public DateTimeOffset DeliveredAt {get;set;}
}
