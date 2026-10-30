using Microsoft.Extensions.Options;
using Npgsql;
using NotificationService;
using RevEx.Infrastructure.Persistence;

namespace RevEx.Api.Subscriptions;

public interface IContentSubscriptions
{
    Task<bool> GetAsync(int contentId, string userId, CancellationToken token);
    Task SetAsync(int contentId, string userId, string? email, bool enabled, CancellationToken token);
}

public sealed class ContentSubscriptions(IOptions<ContentHistoryOptions> options,
    INotificationService notifications, ILogger<ContentSubscriptions> logger)
    : BackgroundService, IContentSubscriptions
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeliverAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Content notification delivery failed; retrying."); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task DeliverAsync(CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(token);
        // A transaction lock prevents simultaneous API instances sending the same batch.
        await using var transaction = await connection.BeginTransactionAsync(token);
        await using var select = new NpgsqlCommand("""
            SELECT q.event_id,q.user_id,q.email,h.content_id,h.description,h.author,h.occurred_at
            FROM content_notification_queue q JOIN content_history_events h ON h.id=q.event_id
            WHERE NOT q.sent ORDER BY q.event_id LIMIT 50 FOR UPDATE OF q SKIP LOCKED
            """, connection, transaction);
        var pending = new List<(long Id,string User,string Email,int Content,string Description,string Author,DateTime Time)>();
        await using (var reader = await select.ExecuteReaderAsync(token))
            while (await reader.ReadAsync(token))
                pending.Add((reader.GetInt64(0),reader.GetString(1),reader.GetString(2),reader.GetInt32(3),
                    reader.GetString(4),reader.GetString(5),reader.GetDateTime(6)));
        foreach (var item in pending)
        {
            try
            {
                await notifications.SendAsync(new EmailNotification([item.Email],
                    $"RevEx: изменение контента {item.Content}",
                    $"{item.Description}\nАвтор: {item.Author}\nВремя (UTC): {item.Time:u}"), token);
                await using var update = new NpgsqlCommand("UPDATE content_notification_queue SET sent=true WHERE event_id=$1 AND user_id=$2", connection, transaction);
                update.Parameters.AddWithValue(item.Id);
                update.Parameters.AddWithValue(item.User);
                await update.ExecuteNonQueryAsync(token);
            }
            catch (Exception exception) when (!token.IsCancellationRequested)
            { logger.LogWarning(exception, "Cannot deliver history event {EventId}", item.Id); }
        }
        await transaction.CommitAsync(token);
    }
}
