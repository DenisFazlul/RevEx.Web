using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.DesktopReleases;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Infrastructure.Repositories;

public sealed class PostgresDesktopUpdateTrackingStore(IOptions<DatabaseOptions> options)
    : IDesktopUpdateTrackingStore, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PostgresContextFactory factory = new(options);

    public async Task ObserveAsync(
        DesktopInstallationRequest request,
        string? userId,
        string? userName,
        CancellationToken cancellationToken)
    {
        Validate(request);

        await using var database = factory.Create();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var keyHash = Hash(request.InstallationKey);
        var affectedCount = await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO desktop_installations(
                installation_id,
                key_hash,
                platform,
                version,
                user_id,
                user_name)
            VALUES(
                {request.InstallationId},
                {keyHash},
                {request.Platform},
                {request.Version},
                {userId},
                {userName})
            ON CONFLICT(installation_id) DO UPDATE
            SET platform = EXCLUDED.platform,
                version = EXCLUDED.version,
                user_id = COALESCE(EXCLUDED.user_id, desktop_installations.user_id),
                user_name = COALESCE(EXCLUDED.user_name, desktop_installations.user_name),
                last_seen_at = CURRENT_TIMESTAMP
            WHERE desktop_installations.key_hash = EXCLUDED.key_hash
            """, cancellationToken);
        if (affectedCount == 0)
        {
            throw new UnauthorizedAccessException("Неверный ключ установки.");
        }

        if (request.CompletedOfferId is { } offerId)
        {
            affectedCount = await database.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE desktop_update_offers
                SET confirmed_at = COALESCE(confirmed_at, CURRENT_TIMESTAMP),
                    failure = NULL
                WHERE id = {offerId}
                    AND installation_id = {request.InstallationId}
                    AND policy->>'targetVersion' = {request.Version}
                    AND policy->'package'->>'platform' = {request.Platform}
                """, cancellationToken);
            if (affectedCount == 0)
            {
                throw new ArgumentException("Подтверждение не соответствует назначенному пакету.");
            }
        }

        if (userId is not null)
        {
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE desktop_update_offers
                SET user_id = {userId}, user_name = {userName}
                WHERE installation_id = {request.InstallationId} AND user_id IS NULL
                """, cancellationToken);
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE desktop_downloads
                SET user_id = {userId}, user_name = {userName}
                WHERE installation_id = {request.InstallationId} AND user_id IS NULL
                """, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<DesktopUpdateOffer> CreateOfferAsync(
        DesktopInstallationRequest request,
        DesktopPolicy policy,
        CancellationToken cancellationToken)
    {
        await using var database = factory.Create();
        var id = Guid.NewGuid();
        var keyHash = Hash(request.InstallationKey);
        var serializedPolicy = JsonSerializer.Serialize(policy, JsonOptions);
        var affectedCount = await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO desktop_update_offers(
                id,
                installation_id,
                from_version,
                policy,
                user_id,
                user_name)
            SELECT
                {id},
                installation_id,
                {request.Version},
                {serializedPolicy}::jsonb,
                user_id,
                user_name
            FROM desktop_installations
            WHERE installation_id = {request.InstallationId} AND key_hash = {keyHash}
            """, cancellationToken);
        if (affectedCount == 0)
        {
            throw new UnauthorizedAccessException();
        }

        var result = await GetOfferAsync(id, request.InstallationKey, cancellationToken);
        return result!;
    }

    public async Task<DesktopUpdateOffer?> GetOfferAsync(
        Guid offerId,
        string installationKey,
        CancellationToken cancellationToken)
    {
        await using var database = factory.Create();
        var keyHash = Hash(installationKey);
        var rows = await database.Database.SqlQuery<DesktopTrackingRow>($"""
            SELECT
                o.id,
                o.installation_id,
                o.from_version,
                o.policy::text,
                o.offered_at,
                o.confirmed_at,
                o.user_id,
                o.user_name,
                o.failure
            FROM desktop_update_offers o
            JOIN desktop_installations i ON i.installation_id = o.installation_id
            WHERE o.id = {offerId} AND i.key_hash = {keyHash}
            """).ToListAsync(cancellationToken);

        return rows.Count == 0 ? null : ToOffer(rows[0]);
    }

    public async Task FailOfferAsync(
        Guid offerId,
        string installationKey,
        string message,
        CancellationToken cancellationToken)
    {
        await using var database = factory.Create();
        var keyHash = Hash(installationKey);
        var affectedCount = await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE desktop_update_offers o
            SET failure = {message}
            FROM desktop_installations i
            WHERE o.id = {offerId}
                AND o.installation_id = i.installation_id
                AND i.key_hash = {keyHash}
                AND o.confirmed_at IS NULL
            """, cancellationToken);
        if (affectedCount == 0)
        {
            throw new ArgumentException("Предложение не найдено или уже подтверждено.");
        }
    }

    public async Task<Guid> StartDownloadAsync(
        DesktopUpdateOffer? offer,
        string version,
        string fileName,
        string? userId,
        string? userName,
        CancellationToken cancellationToken)
    {
        await using var database = factory.Create();
        var id = Guid.NewGuid();
        var persistedUserId = userId ?? offer?.UserId;
        var persistedUserName = userName ?? offer?.UserName;

        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO desktop_downloads(
                id,
                offer_id,
                installation_id,
                version,
                file_name,
                user_id,
                user_name)
            VALUES(
                {id},
                {offer?.Id},
                {offer?.InstallationId},
                {version},
                {fileName},
                {persistedUserId},
                {persistedUserName})
            """, cancellationToken);
        return id;
    }

    public async Task FinishDownloadAsync(
        Guid id,
        string status,
        CancellationToken cancellationToken)
    {
        await using var database = factory.Create();
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE desktop_downloads
            SET finished_at = CURRENT_TIMESTAMP, status = {status}
            WHERE id = {id}
            """, cancellationToken);
    }

    public async Task<DesktopUpdateHistory> GetHistoryAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 500);
        await using var database = factory.Create();
        var installations = await database.Database.SqlQuery<DesktopInstallationRow>($"""
            SELECT installation_id, platform, version, last_seen_at, user_id, user_name
            FROM desktop_installations
            ORDER BY last_seen_at DESC
            LIMIT {normalizedLimit}
            """).ToListAsync(cancellationToken);
        var offers = await database.Database.SqlQuery<DesktopTrackingRow>($"""
            SELECT
                id,
                installation_id,
                from_version,
                policy::text,
                offered_at,
                confirmed_at,
                user_id,
                user_name,
                failure
            FROM desktop_update_offers
            ORDER BY offered_at DESC
            LIMIT {normalizedLimit}
            """).ToListAsync(cancellationToken);
        var downloads = await database.Database.SqlQuery<DesktopDownloadRow>($"""
            SELECT
                id,
                offer_id,
                installation_id,
                version,
                file_name,
                started_at,
                finished_at,
                status,
                user_id,
                user_name
            FROM desktop_downloads
            ORDER BY started_at DESC
            LIMIT {normalizedLimit}
            """).ToListAsync(cancellationToken);

        var installationHistory = installations
            .Select(item => new DesktopInstallationInfo(
                item.InstallationId,
                item.Platform,
                item.Version,
                item.LastSeenAt,
                item.UserId,
                item.UserName))
            .ToList();
        var offerHistory = offers.Select(ToOffer).ToList();
        var downloadHistory = downloads
            .Select(item => new DesktopDownloadInfo(
                item.Id,
                item.OfferId,
                item.InstallationId,
                item.Version,
                item.FileName,
                item.StartedAt,
                item.FinishedAt,
                item.Status,
                item.UserId,
                item.UserName))
            .ToList();

        var result = new DesktopUpdateHistory(installationHistory, offerHistory, downloadHistory);
        return result;
    }

    private static DesktopUpdateOffer ToOffer(DesktopTrackingRow row)
    {
        var policy = JsonSerializer.Deserialize<DesktopPolicy>(row.Policy, JsonOptions)!;
        var result = new DesktopUpdateOffer(
            row.Id,
            row.InstallationId,
            row.FromVersion,
            policy,
            row.OfferedAt,
            row.ConfirmedAt,
            row.UserId,
            row.UserName,
            row.Failure);
        return result;
    }

    private static string Hash(string key)
    {
        var bytes = Encoding.UTF8.GetBytes(key);
        var hash = SHA256.HashData(bytes);
        var result = Convert.ToHexString(hash);
        return result;
    }

    private static void Validate(DesktopInstallationRequest request)
    {
        var hasValidInstallationKey = System.Text.RegularExpressions.Regex.IsMatch(
            request.InstallationKey ?? string.Empty,
            "^[A-Fa-f0-9]{64}$");
        if (request.InstallationId == Guid.Empty || !hasValidInstallationKey)
        {
            throw new ArgumentException("Некорректный идентификатор или ключ установки.");
        }

        DesktopVersions.Parse(request.Version);
        if (request.Platform is not ("win-x64" or "osx-x64" or "osx-arm64"))
        {
            throw new ArgumentException("Платформа не поддерживается.");
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
