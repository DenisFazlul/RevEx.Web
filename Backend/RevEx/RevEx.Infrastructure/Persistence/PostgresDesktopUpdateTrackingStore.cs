using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Npgsql;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.DesktopReleases;

namespace RevEx.Infrastructure.Persistence;

public sealed class PostgresDesktopUpdateTrackingStore : IDesktopUpdateTrackingStore, IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly NpgsqlDataSource _source;

    // Reuse the existing PostgreSQL database and configuration for content history.
    public PostgresDesktopUpdateTrackingStore(IOptions<ContentHistoryOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ConnectionString))
            throw new InvalidOperationException("ContentHistory:ConnectionString must be configured.");
        _source = NpgsqlDataSource.Create(options.Value.ConnectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _source.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // Serialize idempotent schema migration across backend processes.
        await using (var migrationLock = new NpgsqlCommand("SELECT pg_advisory_xact_lock(75211401)", connection, transaction))
            await migrationLock.ExecuteNonQueryAsync(cancellationToken);
        await using var command = new NpgsqlCommand(Migrations.DesktopUpdateSchema.Sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ObserveAsync(DesktopInstallationRequest request, string? userId, string? userName, CancellationToken cancellationToken)
    {
        Validate(request);
        await using var connection = await _source.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        const string sql = """
            INSERT INTO desktop_installations(installation_id, key_hash, platform, version, user_id, user_name)
            VALUES ($1, $2, $3, $4, $5, $6)
            ON CONFLICT (installation_id) DO UPDATE SET
                platform = EXCLUDED.platform, version = EXCLUDED.version,
                user_id = COALESCE(EXCLUDED.user_id, desktop_installations.user_id),
                user_name = COALESCE(EXCLUDED.user_name, desktop_installations.user_name),
                last_seen_at = CURRENT_TIMESTAMP
            WHERE desktop_installations.key_hash = EXCLUDED.key_hash
            RETURNING installation_id;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(request.InstallationId);
        command.Parameters.AddWithValue(Hash(request.InstallationKey));
        command.Parameters.AddWithValue(request.Platform);
        command.Parameters.AddWithValue(request.Version);
        AddNullable(command, userId); AddNullable(command, userName);
        if (await command.ExecuteScalarAsync(cancellationToken) == null) throw new UnauthorizedAccessException("Неверный ключ установки.");
        if (request.CompletedOfferId is { } id)
        {
            await using var confirm = new NpgsqlCommand("""
                UPDATE desktop_update_offers SET confirmed_at = COALESCE(confirmed_at, CURRENT_TIMESTAMP), failure = NULL
                WHERE id = $1 AND installation_id = $2 AND policy->>'targetVersion' = $3
                    AND policy->'package'->>'platform' = $4
                RETURNING id;
                """, connection, transaction);
            confirm.Parameters.AddWithValue(id); confirm.Parameters.AddWithValue(request.InstallationId);
            confirm.Parameters.AddWithValue(request.Version); confirm.Parameters.AddWithValue(request.Platform);
            if (await confirm.ExecuteScalarAsync(cancellationToken) == null)
                throw new ArgumentException("Подтверждение не соответствует назначенному пакету.");
        }
        if (userId != null)
        {
            // Attribute previously anonymous events once, never rewrite their historical author.
            await using var bind = new NpgsqlCommand("""
                WITH attributed AS (
                    UPDATE desktop_update_offers SET user_id = $2, user_name = $3
                    WHERE installation_id = $1 AND user_id IS NULL RETURNING id
                )
                UPDATE desktop_downloads SET user_id = $2, user_name = $3
                WHERE installation_id = $1 AND user_id IS NULL;
                """, connection, transaction);
            bind.Parameters.AddWithValue(request.InstallationId); bind.Parameters.AddWithValue(userId); AddNullable(bind, userName);
            await bind.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<DesktopUpdateOffer> CreateOfferAsync(DesktopInstallationRequest request, DesktopPolicy policy, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await using var command = _source.CreateCommand("""
            INSERT INTO desktop_update_offers(id, installation_id, from_version, policy, user_id, user_name)
            SELECT $1, installation_id, $3, $4::jsonb, user_id, user_name FROM desktop_installations
            WHERE installation_id = $2 AND key_hash = $5
            RETURNING offered_at, user_id, user_name;
            """);
        command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(request.InstallationId);
        command.Parameters.AddWithValue(request.Version); command.Parameters.AddWithValue(JsonSerializer.Serialize(policy, Json));
        command.Parameters.AddWithValue(Hash(request.InstallationKey));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new UnauthorizedAccessException();
        return new(id, request.InstallationId, request.Version, policy, reader.GetFieldValue<DateTimeOffset>(0),
            UserId: NullableString(reader, 1), UserName: NullableString(reader, 2));
    }

    public async Task<DesktopUpdateOffer?> GetOfferAsync(Guid offerId, string installationKey, CancellationToken cancellationToken)
    {
        await using var command = _source.CreateCommand("""
            SELECT o.id, o.installation_id, o.from_version, o.policy::text, o.offered_at, o.confirmed_at, o.user_id, o.user_name, o.failure
            FROM desktop_update_offers o JOIN desktop_installations i ON i.installation_id = o.installation_id
            WHERE o.id = $1 AND i.key_hash = $2;
            """);
        command.Parameters.AddWithValue(offerId); command.Parameters.AddWithValue(Hash(installationKey));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadOffer(reader) : null;
    }

    public async Task FailOfferAsync(Guid offerId, string installationKey, string message, CancellationToken cancellationToken)
    {
        await using var command = _source.CreateCommand("""
            UPDATE desktop_update_offers o SET failure = $3 FROM desktop_installations i
            WHERE o.id = $1 AND o.installation_id = i.installation_id AND i.key_hash = $2 AND o.confirmed_at IS NULL;
            """);
        command.Parameters.AddWithValue(offerId); command.Parameters.AddWithValue(Hash(installationKey)); command.Parameters.AddWithValue(message);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0) throw new ArgumentException("Предложение не найдено или уже подтверждено.");
    }

    public async Task<Guid> StartDownloadAsync(DesktopUpdateOffer? offer, string version, string fileName, string? userId, string? userName, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await using var command = _source.CreateCommand("""
            INSERT INTO desktop_downloads(id, offer_id, installation_id, version, file_name, user_id, user_name)
            VALUES ($1, $2, $3, $4, $5, $6, $7);
            """);
        command.Parameters.AddWithValue(id);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Uuid, Value = (object?)offer?.Id ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Uuid, Value = (object?)offer?.InstallationId ?? DBNull.Value });
        command.Parameters.AddWithValue(version); command.Parameters.AddWithValue(fileName);
        AddNullable(command, userId ?? offer?.UserId); AddNullable(command, userName ?? offer?.UserName);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return id;
    }

    public async Task FinishDownloadAsync(Guid id, string status, CancellationToken cancellationToken)
    {
        await using var command = _source.CreateCommand("UPDATE desktop_downloads SET finished_at = CURRENT_TIMESTAMP, status = $2 WHERE id = $1");
        command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(status);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DesktopUpdateHistory> GetHistoryAsync(int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 500);
        var installations = new List<DesktopInstallationInfo>();
        var updates = new List<DesktopUpdateOffer>();
        var downloads = new List<DesktopDownloadInfo>();
        await using (var command = _source.CreateCommand("SELECT installation_id, platform, version, last_seen_at, user_id, user_name FROM desktop_installations ORDER BY last_seen_at DESC LIMIT $1"))
        {
            command.Parameters.AddWithValue(limit);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                installations.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3), NullableString(reader, 4), NullableString(reader, 5)));
        }
        await using (var command = _source.CreateCommand("SELECT id, installation_id, from_version, policy::text, offered_at, confirmed_at, user_id, user_name, failure FROM desktop_update_offers ORDER BY offered_at DESC LIMIT $1"))
        {
            command.Parameters.AddWithValue(limit);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) updates.Add(ReadOffer(reader));
        }
        await using (var command = _source.CreateCommand("SELECT id, offer_id, installation_id, version, file_name, started_at, finished_at, status, user_id, user_name FROM desktop_downloads ORDER BY started_at DESC LIMIT $1"))
        {
            command.Parameters.AddWithValue(limit);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                downloads.Add(new(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2),
                    reader.GetString(3), reader.GetString(4), reader.GetFieldValue<DateTimeOffset>(5), NullableTime(reader, 6), reader.GetString(7), NullableString(reader, 8), NullableString(reader, 9)));
        }
        return new(installations, updates, downloads);
    }

    private static DesktopUpdateOffer ReadOffer(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2),
        JsonSerializer.Deserialize<DesktopPolicy>(reader.GetString(3), Json)!, reader.GetFieldValue<DateTimeOffset>(4), NullableTime(reader, 5),
        NullableString(reader, 6), NullableString(reader, 7), NullableString(reader, 8));
    private static string? NullableString(NpgsqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
    private static DateTimeOffset? NullableTime(NpgsqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetFieldValue<DateTimeOffset>(index);
    private static void AddNullable(NpgsqlCommand command, string? value) => command.Parameters.Add(new NpgsqlParameter
        { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text, Value = (object?)value ?? DBNull.Value });
    private static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    private static void Validate(DesktopInstallationRequest request)
    {
        if (request.InstallationId == Guid.Empty || !System.Text.RegularExpressions.Regex.IsMatch(request.InstallationKey ?? "", "^[A-Fa-f0-9]{64}$"))
            throw new ArgumentException("Некорректный идентификатор или ключ установки.");
        DesktopVersions.Parse(request.Version);
        if (request.Platform is not ("win-x64" or "osx-x64" or "osx-arm64")) throw new ArgumentException("Платформа не поддерживается.");
    }
    public ValueTask DisposeAsync() => _source.DisposeAsync();
}
