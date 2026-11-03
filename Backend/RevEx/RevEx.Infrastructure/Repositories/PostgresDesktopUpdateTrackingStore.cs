using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.DesktopReleases;
using RevEx.Infrastructure.Persistence;
namespace RevEx.Infrastructure.Repositories;
public sealed class PostgresDesktopUpdateTrackingStore(IOptions<ContentHistoryOptions> options) : IDesktopUpdateTrackingStore, IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly PostgresContextFactory factory = new(options);
    public async Task ObserveAsync(DesktopInstallationRequest request, string? userId, string? userName, CancellationToken cancellationToken)
    {
        Validate(request);
        await using var db = factory.Create();
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO desktop_installations(installation_id,key_hash,platform,version,user_id,user_name)
            VALUES({request.InstallationId},{Hash(request.InstallationKey)},{request.Platform},{request.Version},{userId},{userName})
            ON CONFLICT(installation_id) DO UPDATE SET platform=EXCLUDED.platform,version=EXCLUDED.version,
                user_id=COALESCE(EXCLUDED.user_id,desktop_installations.user_id),
                user_name=COALESCE(EXCLUDED.user_name,desktop_installations.user_name),last_seen_at=CURRENT_TIMESTAMP
            WHERE desktop_installations.key_hash=EXCLUDED.key_hash
            """, cancellationToken);
        if (affected == 0) throw new UnauthorizedAccessException("Неверный ключ установки.");
        if (request.CompletedOfferId is {} id)
        {
            affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE desktop_update_offers SET confirmed_at=COALESCE(confirmed_at,CURRENT_TIMESTAMP),failure=NULL
                WHERE id={id} AND installation_id={request.InstallationId} AND policy->>'targetVersion'={request.Version}
                    AND policy->'package'->>'platform'={request.Platform}
                """, cancellationToken);
            if (affected == 0) throw new ArgumentException("Подтверждение не соответствует назначенному пакету.");
        }
        if (userId != null)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE desktop_update_offers SET user_id={userId},user_name={userName} WHERE installation_id={request.InstallationId} AND user_id IS NULL", cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE desktop_downloads SET user_id={userId},user_name={userName} WHERE installation_id={request.InstallationId} AND user_id IS NULL", cancellationToken);
        }
        await tx.CommitAsync(cancellationToken);
    }
    public async Task<DesktopUpdateOffer> CreateOfferAsync(DesktopInstallationRequest request, DesktopPolicy policy, CancellationToken cancellationToken)
    {
        await using var db = factory.Create();
        var id = Guid.NewGuid();
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO desktop_update_offers(id,installation_id,from_version,policy,user_id,user_name)
            SELECT {id},installation_id,{request.Version},{JsonSerializer.Serialize(policy, Json)}::jsonb,user_id,user_name
            FROM desktop_installations WHERE installation_id={request.InstallationId} AND key_hash={Hash(request.InstallationKey)}
            """, cancellationToken);
        if (affected == 0) throw new UnauthorizedAccessException();
        return (await GetOfferAsync(id, request.InstallationKey, cancellationToken))!;
    }
    public async Task<DesktopUpdateOffer?> GetOfferAsync(Guid offerId, string installationKey, CancellationToken cancellationToken)
    {
        await using var db = factory.Create();
        var rows = await db.Database.SqlQuery<DesktopTrackingRow>($"""
            SELECT o.id,o.installation_id,o.from_version,o.policy::text,o.offered_at,o.confirmed_at,o.user_id,o.user_name,o.failure
            FROM desktop_update_offers o JOIN desktop_installations i ON i.installation_id=o.installation_id
            WHERE o.id={offerId} AND i.key_hash={Hash(installationKey)}
            """).ToListAsync(cancellationToken);
        return rows.Count == 0 ? null : ToOffer(rows[0]);
    }
    public async Task FailOfferAsync(Guid offerId, string installationKey, string message, CancellationToken cancellationToken)
    {
        await using var db = factory.Create();
        if (await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE desktop_update_offers o SET failure={message} FROM desktop_installations i
            WHERE o.id={offerId} AND o.installation_id=i.installation_id AND i.key_hash={Hash(installationKey)} AND o.confirmed_at IS NULL
            """, cancellationToken) == 0) throw new ArgumentException("Предложение не найдено или уже подтверждено.");
    }
    public async Task<Guid> StartDownloadAsync(DesktopUpdateOffer? offer, string version, string fileName, string? userId, string? userName, CancellationToken cancellationToken)
    {
        await using var db = factory.Create();
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO desktop_downloads(id,offer_id,installation_id,version,file_name,user_id,user_name)
            VALUES({id},{offer?.Id},{offer?.InstallationId},{version},{fileName},{userId ?? offer?.UserId},{userName ?? offer?.UserName})
            """, cancellationToken);
        return id;
    }
    public async Task FinishDownloadAsync(Guid id, string status, CancellationToken cancellationToken)
    {
        await using var db = factory.Create();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE desktop_downloads SET finished_at=CURRENT_TIMESTAMP,status={status} WHERE id={id}", cancellationToken);
    }
    public async Task<DesktopUpdateHistory> GetHistoryAsync(int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 500);
        await using var db = factory.Create();
        var installations = await db.Database.SqlQuery<DesktopInstallationRow>($"SELECT installation_id,platform,version,last_seen_at,user_id,user_name FROM desktop_installations ORDER BY last_seen_at DESC LIMIT {limit}").ToListAsync(cancellationToken);
        var offers = await db.Database.SqlQuery<DesktopTrackingRow>($"SELECT id,installation_id,from_version,policy::text,offered_at,confirmed_at,user_id,user_name,failure FROM desktop_update_offers ORDER BY offered_at DESC LIMIT {limit}").ToListAsync(cancellationToken);
        var downloads = await db.Database.SqlQuery<DesktopDownloadRow>($"SELECT id,offer_id,installation_id,version,file_name,started_at,finished_at,status,user_id,user_name FROM desktop_downloads ORDER BY started_at DESC LIMIT {limit}").ToListAsync(cancellationToken);
        return new(installations.Select(x => new DesktopInstallationInfo(x.InstallationId,x.Platform,x.Version,x.LastSeenAt,x.UserId,x.UserName)).ToList(),
            offers.Select(ToOffer).ToList(), downloads.Select(x => new DesktopDownloadInfo(x.Id,x.OfferId,x.InstallationId,x.Version,x.FileName,x.StartedAt,x.FinishedAt,x.Status,x.UserId,x.UserName)).ToList());
    }
    private static DesktopUpdateOffer ToOffer(DesktopTrackingRow x) => new(x.Id,x.InstallationId,x.FromVersion,JsonSerializer.Deserialize<DesktopPolicy>(x.Policy,Json)!,x.OfferedAt,x.ConfirmedAt,x.UserId,x.UserName,x.Failure);
    private static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    private static void Validate(DesktopInstallationRequest request)
    {
        if (request.InstallationId == Guid.Empty || !System.Text.RegularExpressions.Regex.IsMatch(request.InstallationKey ?? "", "^[A-Fa-f0-9]{64}$")) throw new ArgumentException("Некорректный идентификатор или ключ установки.");
        DesktopVersions.Parse(request.Version);
        if (request.Platform is not ("win-x64" or "osx-x64" or "osx-arm64")) throw new ArgumentException("Платформа не поддерживается.");
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
