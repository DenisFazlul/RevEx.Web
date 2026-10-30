using Microsoft.Extensions.Options;
using Npgsql;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Api.Subscriptions;

public interface IContentSubscriptions
{
    Task<bool> GetAsync(int contentId, string userId, CancellationToken token);
    Task SetAsync(int contentId, string userId, string? email, bool enabled, CancellationToken token);
}

public sealed class ContentSubscriptions(IOptions<ContentHistoryOptions> options) : IContentSubscriptions
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public async Task<bool> GetAsync(int contentId, string userId, CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM content_subscriptions WHERE content_id=$1 AND user_id=$2)", connection);
        command.Parameters.AddWithValue(contentId);
        command.Parameters.AddWithValue(userId);
        return (bool)(await command.ExecuteScalarAsync(token))!;
    }

    public async Task SetAsync(int contentId, string userId, string? email, bool enabled, CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand(enabled
            ? "INSERT INTO content_subscriptions(content_id,user_id,email) VALUES($1,$2,$3) ON CONFLICT(content_id,user_id) DO UPDATE SET email=EXCLUDED.email"
            : "DELETE FROM content_subscriptions WHERE content_id=$1 AND user_id=$2", connection);
        command.Parameters.AddWithValue(contentId);
        command.Parameters.AddWithValue(userId);
        if (enabled) command.Parameters.AddWithValue(email!);
        await command.ExecuteNonQueryAsync(token);
    }
}
