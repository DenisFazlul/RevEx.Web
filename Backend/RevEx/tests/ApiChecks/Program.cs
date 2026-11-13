using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using RevEx.Api.Controllers;
using RevEx.Api.Extensions;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Identifiers;
using RevEx.Infrastructure.Persistence;
using RevEx.Infrastructure.Repositories;
using RevEx.Infrastructure.Storage;

var connectionString = Environment.GetEnvironmentVariable("REVEX_TEST_POSTGRES")
    ?? "Host=localhost;Port=5433;Database=revex_history;Username=revex;Password=revex";
var schema = "api_checks_" + Guid.NewGuid().ToString("N");
var isolatedConnectionString = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
{
    SearchPath = schema
}.ConnectionString;

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Logging.ClearProviders();
builder.Services
    .AddApiControllers()
    .AddControllers()
    .AddApplicationPart(typeof(ContentController).Assembly);
builder.Services.AddScoped(_ => new PostgresDbContext(
    new DbContextOptionsBuilder<PostgresDbContext>()
        .UseNpgsql(
            isolatedConnectionString,
            postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", schema))
        .Options));
builder.Services.AddScoped<IContentRepository, PostgresContentRepository>();
builder.Services.AddScoped<IContentFileRepository, PostgresContentFileRepository>();
builder.Services.AddScoped<IContentFileMetadataRepository, PostgresContentFileMetadataRepository>();
builder.Services.AddScoped<ITagGroupRepository, PostgresTagGroupRepository>();
builder.Services.AddScoped<IContentStatusRepository, PostgresContentStatusRepository>();
builder.Services.AddScoped<ICategoryRepository, PostgresCategoryRepository>();
builder.Services.AddScoped<ITagRepository, PostgresTagRepository>();
builder.Services.AddScoped<IContentVersionRepository, PostgresContentVersionRepository>();
builder.Services.AddScoped<IContentQueries, PostgresContentQueries>();
builder.Services.AddSingleton<RevEx.Application.Subscriptions.IContentSubscriptions, Subscriptions>();
builder.Services.AddSingleton<IFileStorage, InMemoryFileStorage>();
builder.Services.AddSingleton<IContentHistoryStore, History>();
builder.Services
    .AddAuthentication("Checks")
    .AddScheme<AuthenticationSchemeOptions, ChecksAuthentication>("Checks", _ =>
    {
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("EditContent", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("AdministerCatalog", policy => policy.RequireAuthenticatedUser());
});

await using var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

try
{
    await CreateSchemaAsync(connectionString, schema);
    using (var scope = app.Services.CreateScope())
    {
        var database = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        await database.Database.MigrateAsync();
    }

    await app.StartAsync();
    using var client = new HttpClient
    {
        BaseAddress = new Uri(app.Urls.Single())
    };

    var categoryResponse = await client.PostAsJsonAsync("/api/categories", new { name = "Checks" });
    await ExpectAsync(categoryResponse, HttpStatusCode.Created);
    var category = await categoryResponse.Content.ReadFromJsonAsync<RevEx.Domain.Contracts.Category.CategoryDto>()
        ?? throw new InvalidOperationException("Category response is empty.");

    var statusResponse = await client.PostAsJsonAsync("/api/content-statuses", new { name = "Draft" });
    await ExpectAsync(statusResponse, HttpStatusCode.Created);
    var status = await statusResponse.Content.ReadFromJsonAsync<RevEx.Domain.Contracts.ContentStatus.ContentStatusDto>()
        ?? throw new InvalidOperationException("Content status response is empty.");

    var contentResponse = await client.PostAsJsonAsync("/api/content", new CreateContentDto
    {
        Cid = Uuid7.Create(),
        Name = "PostgreSQL check",
        Description = "EF Core API smoke test",
        CategoryId = category.Id,
        ContentStatusId = status.Id
    });
    await ExpectAsync(contentResponse, HttpStatusCode.Created);
    var content = await contentResponse.Content.ReadFromJsonAsync<ContentDto>()
        ?? throw new InvalidOperationException("Content response is empty.");

    var query = await client.GetFromJsonAsync<ContentDto[]>(
        "/api/content/query?Name=PostgreSQL&Limit=1");
    if (query is null || query.Length != 1 || query[0].Id != content.Id)
    {
        throw new InvalidOperationException("PostgreSQL content query failed.");
    }

    Console.WriteLine("PASS: authenticated HTTP CRUD and query through EF Core PostgreSQL.");
}
finally
{
    await app.StopAsync();
    await DropSchemaAsync(connectionString, schema);
}

static async Task CreateSchemaAsync(string connectionString, string schema)
{
    await using var connection = new Npgsql.NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = $"CREATE SCHEMA \"{schema}\"";
    await command.ExecuteNonQueryAsync();
}

static async Task DropSchemaAsync(string connectionString, string schema)
{
    await using var connection = new Npgsql.NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
    await command.ExecuteNonQueryAsync();
}

static async Task ExpectAsync(HttpResponseMessage response, HttpStatusCode status)
{
    if (response.StatusCode != status)
    {
        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"Expected {status}, got {response.StatusCode}: {body}");
    }
}
