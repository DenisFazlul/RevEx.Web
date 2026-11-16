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
    ?? throw new InvalidOperationException(
        "Set REVEX_TEST_POSTGRES to an isolated PostgreSQL database.");
var schema = "api_checks_" + Guid.NewGuid().ToString("N");
var isolatedConnectionString = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
{
    SearchPath = $"{schema},public"
}.ConnectionString;
var commandCounter = new CommandCounterInterceptor();

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
        .AddInterceptors(commandCounter)
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

    await VerifyOptimisticConcurrencyAsync(app.Services, content.Id);
    await VerifyFileMetadataTransactionAsync(app.Services);

    var query = await client.GetFromJsonAsync<ContentDto[]>(
        "/api/content/query?Name=PostgreSQL&Limit=1");
    if (query is null || query.Length != 1 || query[0].Id != content.Id)
    {
        throw new InvalidOperationException("PostgreSQL content query failed.");
    }

    await VerifyBoundedQueryCountAsync(app.Services, client, category.Id, status.Id, commandCounter);

    Console.WriteLine("PASS: authenticated PostgreSQL HTTP CRUD, query, constraints and bounded query counts.");
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

static async Task VerifyBoundedQueryCountAsync(
    IServiceProvider services,
    HttpClient client,
    int categoryId,
    int statusId,
    CommandCounterInterceptor counter)
{
    int[] tagIds;
    using (var scope = services.CreateScope())
    {
        var groups = scope.ServiceProvider.GetRequiredService<ITagGroupRepository>();
        var tags = scope.ServiceProvider.GetRequiredService<ITagRepository>();
        var group = await groups.AddAsync(new RevEx.Domain.Entities.TagGroup
        {
            Name = "N+1 checks"
        }, default);
        var createdTags = new List<RevEx.Domain.Entities.Tag>();
        for (var index = 0; index < 20; index++)
        {
            var tag = await tags.AddAsync(new RevEx.Domain.Entities.Tag
            {
                Name = $"N+1 tag {index}",
                TagGroupId = group.Id
            }, default);
            createdTags.Add(tag);
        }

        tagIds = createdTags.Select(tag => tag.Id).ToArray();
    }

    counter.Reset();
    var createResponse = await client.PostAsJsonAsync("/api/content", new CreateContentDto
    {
        Cid = Uuid7.Create(),
        Name = "N+1 bounded content",
        Description = "Twenty tags must be resolved in one query.",
        CategoryId = categoryId,
        ContentStatusId = statusId,
        TagIds = tagIds
    });
    await ExpectAsync(createResponse, HttpStatusCode.Created);
    if (counter.CommandCount > 9)
    {
        throw new InvalidOperationException(
            $"Content creation issued {counter.CommandCount} database commands for 20 tags.");
    }

    counter.Reset();
    var contents = await client.GetFromJsonAsync<ContentDto[]>("/api/content");
    if (contents is null || contents.Length < 2)
    {
        throw new InvalidOperationException("Content list check returned an incomplete result.");
    }

    if (counter.CommandCount != 1)
    {
        throw new InvalidOperationException(
            $"Content list issued {counter.CommandCount} database commands instead of one.");
    }

    counter.Reset();
    var deleteResponse = await client.DeleteAsync($"/api/tags/{tagIds[0]}");
    await ExpectAsync(deleteResponse, HttpStatusCode.NoContent);
    if (counter.CommandCount > 6)
    {
        throw new InvalidOperationException(
            $"Tag deletion issued {counter.CommandCount} database commands for related content.");
    }
}

static async Task VerifyOptimisticConcurrencyAsync(IServiceProvider services, int contentId)
{
    using var firstScope = services.CreateScope();
    using var secondScope = services.CreateScope();
    var firstRepository = firstScope.ServiceProvider.GetRequiredService<IContentRepository>();
    var secondRepository = secondScope.ServiceProvider.GetRequiredService<IContentRepository>();
    var first = await firstRepository.GetByIdAsync(contentId, default)
        ?? throw new InvalidOperationException("The concurrency fixture was not found.");
    var second = await secondRepository.GetByIdAsync(contentId, default)
        ?? throw new InvalidOperationException("The concurrency fixture was not found.");

    first.Description = "First concurrent update";
    await firstRepository.UpdateAsync(first, default);
    second.Description = "Stale concurrent update";

    try
    {
        await secondRepository.UpdateAsync(second, default);
        throw new InvalidOperationException("A stale update overwrote a newer value.");
    }
    catch (PersistenceConflictException)
    {
    }
}

static async Task VerifyFileMetadataTransactionAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var files = scope.ServiceProvider.GetRequiredService<IContentFileRepository>();
    var before = (await files.GetAllAsync(default)).Count;
    var file = new RevEx.Domain.Entities.ContentFile
    {
        Url = "pending",
        OriginalFileName = "invalid-metadata.rfa",
        Extension = ".rfa",
        SizeBytes = 1,
        Role = "attachment",
        MetadataJson = "[]"
    };

    try
    {
        await files.AddAsync(file, default);
        throw new InvalidOperationException("Non-object JSON metadata passed the database constraint.");
    }
    catch (Microsoft.EntityFrameworkCore.DbUpdateException)
    {
    }

    var after = (await files.GetAllAsync(default)).Count;
    if (after != before)
    {
        throw new InvalidOperationException("A failed metadata insert left an orphan file record.");
    }
}
