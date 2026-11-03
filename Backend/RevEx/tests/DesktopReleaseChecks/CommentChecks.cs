using Microsoft.EntityFrameworkCore;
using RevEx.Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using RevEx.Infrastructure.Persistence;
internal static class CommentChecks
{
    public static async Task RunAsync(string connectionString)
    {
        await using var admin = Npgsql.NpgsqlDataSource.Create(connectionString);
        var schema = "comment_checks_" + Guid.NewGuid().ToString("N");
        await using(var command = admin.CreateCommand($"CREATE SCHEMA {schema}")) await command.ExecuteNonQueryAsync();
        try
        {
            var options = Options.Create(new ContentHistoryOptions { ConnectionString = new Npgsql.NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString });
            await using (var db = new PostgresContextFactory(options).Create())
                await db.Database.MigrateAsync();
            var store = new PostgresCommentStore(options);
            var set = await store.CreateSetAsync("Discussion","user-1","Author",default);
            var comment = await store.AddAsync(set.Id,"Hello","user-1","Author",default) ?? throw new Exception("Add failed");
            if (await store.UpdateAsync(set.Id,comment.Id,"Intrusion","user-2",default) != null || await store.DeleteAsync(set.Id,comment.Id,"user-2",default)) throw new Exception("Author protection failed");
            var edited = await store.UpdateAsync(set.Id,comment.Id,"Edited","user-1",default);
            if (edited?.UpdatedAt == null || edited.CreatedAt != comment.CreatedAt) throw new Exception("Audit failed");
            var persisted = await new PostgresCommentStore(options).GetCommentsAsync(set.Id,0,50,default);
            if (persisted?.Single().Text != "Edited") throw new Exception("Persistence failed");
            if ((await store.GetCommentsAsync(set.Id,1,50,default))?.Count != 0) throw new Exception("Pagination failed");
            if (await store.AddAsync(Guid.NewGuid(),"Missing","user-1","Author",default) != null) throw new Exception("Missing set accepted");
            if (!await store.DeleteAsync(set.Id,comment.Id,"user-1",default)) throw new Exception("Delete failed");
            Console.WriteLine("PASS: Comment sets, persistence, author protection, audit, pagination and deletion");
        }
        finally
        {
            await using var command = admin.CreateCommand($"DROP SCHEMA {schema} CASCADE");
            await command.ExecuteNonQueryAsync();
        }
    }
}
