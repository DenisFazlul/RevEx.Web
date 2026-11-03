using System.ComponentModel.DataAnnotations.Schema;
namespace RevEx.Infrastructure.Persistence;
public sealed class DesktopTrackingRow
{
    [Column("id")] public Guid Id { get; set; }
    [Column("installation_id")] public Guid InstallationId { get; set; }
    [Column("from_version")] public string FromVersion { get; set; } = "";
    [Column("policy")] public string Policy { get; set; } = "";
    [Column("offered_at")] public DateTimeOffset OfferedAt { get; set; }
    [Column("confirmed_at")] public DateTimeOffset? ConfirmedAt { get; set; }
    [Column("user_id")] public string? UserId { get; set; }
    [Column("user_name")] public string? UserName { get; set; }
    [Column("failure")] public string? Failure { get; set; }
}
