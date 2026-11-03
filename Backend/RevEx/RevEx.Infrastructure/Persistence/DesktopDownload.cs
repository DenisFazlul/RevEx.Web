namespace RevEx.Infrastructure.Persistence;
public sealed class DesktopDownload
{
public Guid Id {get;set;} public Guid? OfferId {get;set;} public Guid? InstallationId {get;set;} public required string Version {get;set;} public required string FileName {get;set;} public DateTimeOffset StartedAt {get;set;} public DateTimeOffset? FinishedAt {get;set;} public required string Status {get;set;} public string? UserId {get;set;} public string? UserName {get;set;}
}
