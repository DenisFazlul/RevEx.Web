using System.ComponentModel.DataAnnotations.Schema;
namespace RevEx.Infrastructure.Persistence;
public sealed class DesktopDownloadRow
{
    [Column("id")] public Guid Id { get; set; }
    [Column("offer_id")] public Guid? OfferId { get; set; }
    [Column("installation_id")] public Guid? InstallationId { get; set; }
    [Column("version")] public required string Version { get; set; }
    [Column("file_name")] public required string FileName { get; set; }
    [Column("started_at")] public DateTimeOffset StartedAt { get; set; }
    [Column("finished_at")] public DateTimeOffset? FinishedAt { get; set; }
    [Column("status")] public required string Status { get; set; }
    [Column("user_id")] public string? UserId { get; set; }
    [Column("user_name")] public string? UserName { get; set; }
}
