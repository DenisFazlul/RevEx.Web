using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace RevEx.Infrastructure.Persistence;

// Each operation owns a context; singleton stores and background workers never share a DbContext.
public sealed class PostgresContextFactory(IOptions<ContentHistoryOptions> options)
{
    private readonly DbContextOptions<PostgresDbContext> settings = new DbContextOptionsBuilder<PostgresDbContext>()
        .UseNpgsql(
            options.Value.ConnectionString,
            postgres => postgres.MigrationsHistoryTable(
                "__EFMigrationsHistory",
                new Npgsql.NpgsqlConnectionStringBuilder(options.Value.ConnectionString)
                    .SearchPath?
                    .Split(',')[0]
                    .Trim() ?? "public"))
        .Options;
    public PostgresDbContext Create() => new(settings);
}
