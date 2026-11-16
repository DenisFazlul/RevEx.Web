using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RevEx.Infrastructure.Persistence;
using RevEx.Infrastructure.Repositories;

internal static class CommentChecks
{
    public static async Task RunAsync(string connectionString)
    {
        await using var admin = Npgsql.NpgsqlDataSource.Create(connectionString);
        var schema = "comment_checks_" + Guid.NewGuid().ToString("N");

        await using (var command = admin.CreateCommand($"CREATE SCHEMA {schema}"))
        {
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            var isolatedConnectionString = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = $"{schema},public"
            }.ConnectionString;
            var options = Options.Create(new DatabaseOptions
            {
                ConnectionString = isolatedConnectionString
            });

            await using (var database = new PostgresContextFactory(options).Create())
            {
                await database.Database.MigrateAsync();
            }

            var store = new PostgresCommentStore(options);
            var set = await store.CreateSetAsync("Discussion", "user-1", "Author", default);
            var comment = await store.AddAsync(set.Id, "Hello", "user-1", "Author", default)
                ?? throw new InvalidOperationException("Add failed");
            var unauthorizedUpdate = await store.UpdateAsync(
                set.Id,
                comment.Id,
                "Intrusion",
                "user-2",
                default);
            var unauthorizedDelete = await store.DeleteAsync(
                set.Id,
                comment.Id,
                "user-2",
                default);
            if (unauthorizedUpdate is not null || unauthorizedDelete)
            {
                throw new InvalidOperationException("Author protection failed");
            }

            var edited = await store.UpdateAsync(
                set.Id,
                comment.Id,
                "Edited",
                "user-1",
                default);
            if (edited?.UpdatedAt is null || edited.CreatedAt != comment.CreatedAt)
            {
                throw new InvalidOperationException("Audit failed");
            }

            var restartedStore = new PostgresCommentStore(options);
            var persisted = await restartedStore.GetCommentsAsync(set.Id, 0, 50, default);
            if (persisted?.Single().Text != "Edited")
            {
                throw new InvalidOperationException("Persistence failed");
            }

            var secondPage = await store.GetCommentsAsync(set.Id, 1, 50, default);
            if (secondPage?.Count != 0)
            {
                throw new InvalidOperationException("Pagination failed");
            }

            var orphan = await store.AddAsync(
                Guid.NewGuid(),
                "Missing",
                "user-1",
                "Author",
                default);
            if (orphan is not null)
            {
                throw new InvalidOperationException("Missing set accepted");
            }

            var deleted = await store.DeleteAsync(set.Id, comment.Id, "user-1", default);
            if (!deleted)
            {
                throw new InvalidOperationException("Delete failed");
            }

            Console.WriteLine("PASS: Comment sets, persistence, author protection, audit, pagination and deletion");
        }
        finally
        {
            await using var command = admin.CreateCommand($"DROP SCHEMA {schema} CASCADE");
            await command.ExecuteNonQueryAsync();
        }
    }
}
