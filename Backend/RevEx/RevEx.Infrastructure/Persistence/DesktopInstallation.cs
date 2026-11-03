namespace RevEx.Infrastructure.Persistence;
public sealed class DesktopInstallation
{
public Guid InstallationId {get;set;} public required string KeyHash {get;set;} public required string Platform {get;set;} public required string Version {get;set;} public string? UserId {get;set;} public string? UserName {get;set;} public DateTimeOffset LastSeenAt {get;set;}
}
