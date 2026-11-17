using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using RevEx.Api.Controllers;
using RevEx.Api.Extensions;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentVersion;
using RevEx.Domain.Entities;
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
builder.Services.AddScoped<IContentParameterRepository, PostgresContentParameterRepository>();
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
    options.AddPolicy("AdministerCatalog", policy => policy.RequireRole(RevEx.Domain.Security.RevExRoles.Administrator));
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

    await VerifyContentFreezeAsync(client, content);
    await VerifyContentParametersAsync(client, content.Id);

    foreach (var (platform, name) in new[] { (Platform.AutoCad, "drawing.dwg"), (Platform.Other, "notes.txt"), (Platform.Revit, "family.rfa") })
    {
        var vid = Uuid7.Create();
        using var metadata = JsonContent.Create(new { CID = new { Value = content.Cid }, VID = new { Value = vid } });
        var json = await metadata.ReadAsStringAsync();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(content.Cid.ToString("D")), "Cid");
            form.Add(new StringContent(vid.ToString("D")), "Vid");
            form.Add(new StringContent(content.Name), "ContentName");
            form.Add(new StringContent("Version import"), "VersionName");
            form.Add(new StringContent("2026"), "ApplicationVersion");
            form.Add(new StringContent(platform.ToString()), "Application");
            form.Add(new StringContent(platform.ToString()), "Platform");
            form.Add(new StringContent(DateTimeOffset.UtcNow.ToString("O")), "Date");
            form.Add(new StringContent(json), "MetadataJson");
            form.Add(new ByteArrayContent([1]), "File", name);
            form.Add(new StringContent("{}"), "AuxiliaryMetadataJson[0]");
            form.Add(new ByteArrayContent([2]), "AuxiliaryFiles", "extra.txt");
            using var response = await client.PostAsync("/api/imports/versions", form);
            await ExpectAsync(response, HttpStatusCode.OK);
            var version = await response.Content.ReadFromJsonAsync<ContentVersionDto>();
            if (version?.Vid != vid || version.Platform != platform || version.FileIds.Count != 2)
            {
                throw new InvalidOperationException($"Import for {platform} returned {await response.Content.ReadAsStringAsync()}.");
            }
        }
    }

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

static async Task VerifyContentParametersAsync(HttpClient client, int contentId)
{
    var collection = $"/api/content/{contentId}/parameters";
    var input = new ContentParameterInput("Ширина", "number", "12.5", "Размер в миллиметрах.");
    using var created = await client.PostAsJsonAsync(collection, input);
    await ExpectAsync(created, HttpStatusCode.Created);
    var parameter = await created.Content.ReadFromJsonAsync<ContentParameterDto>()
        ?? throw new InvalidOperationException("Parameter response is empty.");
    var item = $"{collection}/{parameter.Id}";

    using var invalid = await client.PutAsJsonAsync(item, input with { Value = "text" });
    await ExpectAsync(invalid, HttpStatusCode.BadRequest);
    using var wrongContent = await client.DeleteAsync($"/api/content/{contentId + 1}/parameters/{parameter.Id}");
    await ExpectAsync(wrongContent, HttpStatusCode.NotFound);

    var updated = input with { Kind = "link", Value = "https://example.test/document" };
    using var update = await client.PutAsJsonAsync(item, updated);
    await ExpectAsync(update, HttpStatusCode.NoContent);
    var persisted = await client.GetFromJsonAsync<ContentParameterDto[]>(collection);
    if (persisted?.SingleOrDefault() != parameter with { Kind = updated.Kind, Value = updated.Value })
    {
        throw new InvalidOperationException("Parameter and tooltip did not round-trip.");
    }

    using var deleted = await client.DeleteAsync(item);
    await ExpectAsync(deleted, HttpStatusCode.NoContent);
    using var missing = await client.GetAsync(item);
    await ExpectAsync(missing, HttpStatusCode.NotFound);
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

static async Task VerifyContentFreezeAsync(HttpClient client, ContentDto content)
{
    var route = $"/api/content/{content.Id}";
    using var moderatorRequest = new HttpRequestMessage(HttpMethod.Put, $"{route}/freeze")
    {
        Content = JsonContent.Create(true)
    };
    moderatorRequest.Headers.Add("X-Check-Moderator", "1");
    using var forbidden = await client.SendAsync(moderatorRequest);
    await ExpectAsync(forbidden, HttpStatusCode.Forbidden);

    using var frozen = await client.PutAsJsonAsync($"{route}/freeze", true);
    await ExpectAsync(frozen, HttpStatusCode.NoContent);

    using var edit = await client.PutAsJsonAsync(route, new UpdateContentDto
    {
        Name = content.Name,
        Description = content.Description,
        CategoryId = content.CategoryId,
        ContentStatusId = content.ContentStatusId,
        TagIds = content.TagIds
    });
    await ExpectAsync(edit, HttpStatusCode.NoContent);
    var updated = await client.GetFromJsonAsync<ContentDto>(route);
    if (updated?.IsFrozen != true)
    {
        throw new InvalidOperationException("The content update cleared the freeze status.");
    }

    using var thawed = await client.PutAsJsonAsync($"{route}/freeze", false);
    await ExpectAsync(thawed, HttpStatusCode.NoContent);
    updated = await client.GetFromJsonAsync<ContentDto>(route);
    if (updated?.IsFrozen != false)
    {
        throw new InvalidOperationException("The administrator could not thaw the content.");
    }
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
