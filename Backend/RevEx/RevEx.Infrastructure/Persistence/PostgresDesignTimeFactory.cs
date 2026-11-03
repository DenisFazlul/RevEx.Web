using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace RevEx.Infrastructure.Persistence;
public sealed class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresDbContext>
{
    public PostgresDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql("Host=localhost;Database=revex_history").Options);
}
