using System.ComponentModel.DataAnnotations.Schema;
namespace RevEx.Infrastructure.Persistence;
public sealed class DesktopInstallationRow
{
    [Column("installation_id")] public Guid InstallationId { get; set; }
    [Column("platform")] public required string Platform { get; set; }
    [Column("version")] public required string Version { get; set; }
    [Column("last_seen_at")] public DateTimeOffset LastSeenAt { get; set; }
    [Column("user_id")] public string? UserId { get; set; }
    [Column("user_name")] public string? UserName { get; set; }
}
