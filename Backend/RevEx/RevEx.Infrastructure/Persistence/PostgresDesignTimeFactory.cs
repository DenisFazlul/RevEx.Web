using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RevEx.Infrastructure.Persistence;

public sealed class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresDbContext>
{
    public PostgresDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("Database__ConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set Database__ConnectionString before running EF Core tools.");
        }

        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PostgresDbContext(options);
    }
}
