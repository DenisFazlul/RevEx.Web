using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;
namespace RevEx.Infrastructure.Persistence;

#pragma warning disable EF1001 // Required by the Npgsql migration generator extension point.
// Initial EF adoption must tolerate tables created by earlier releases.
public sealed class LegacyPostgresMigrationsSqlGenerator(
    MigrationsSqlGeneratorDependencies dependencies,
    INpgsqlSingletonOptions options, Microsoft.EntityFrameworkCore.Storage.IRawSqlCommandBuilder rawSql) : NpgsqlMigrationsSqlGenerator(dependencies, options)
{
    public override IReadOnlyList<MigrationCommand> Generate(IReadOnlyList<MigrationOperation> operations, Microsoft.EntityFrameworkCore.Metadata.IModel? model = null, MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        return base.Generate(operations, model, options).Select(command => new MigrationCommand(
            rawSql.Build(command.CommandText
                .Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ")
                .Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ")
                .Replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS ")),
            Dependencies.CurrentContext.Context, Dependencies.Logger, command.TransactionSuppressed)).ToArray();
    }
}
