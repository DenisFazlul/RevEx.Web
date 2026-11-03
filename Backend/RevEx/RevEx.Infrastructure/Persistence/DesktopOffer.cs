namespace RevEx.Infrastructure.Persistence;
public sealed class DesktopOffer
{
public Guid Id {get;set;} public Guid InstallationId {get;set;} public required string FromVersion {get;set;} public required string Policy {get;set;} public DateTimeOffset OfferedAt {get;set;} public DateTimeOffset? ConfirmedAt {get;set;} public string? UserId {get;set;} public string? UserName {get;set;} public string? Failure {get;set;}
}
