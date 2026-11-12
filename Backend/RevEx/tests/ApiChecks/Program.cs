using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RevEx.Api.Controllers;
using RevEx.Api.Extensions;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Domain.Entities;
using RevEx.Domain.Identifiers;
using RevEx.Infrastructure.Persistence;
using RevEx.Infrastructure.Repositories;
using RevEx.Infrastructure.Storage;

var database = "revex_api_checks_" + Guid.NewGuid().ToString("N");
var builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Logging.ClearProviders();
builder.Services.AddApiControllers().AddControllers().AddApplicationPart(typeof(ContentController).Assembly);
builder.Services.AddSingleton<IOptions<MongoDbOptions>>(Options.Create(new MongoDbOptions { ConnectionString = "mongodb://localhost:27017", DatabaseName = database }));
builder.Services.AddScoped<MongoDbContext>();
builder.Services.AddScoped<IContentRepository, MongoContentRepository>();
builder.Services.AddScoped<IContentFileRepository, MongoContentFileRepository>();
builder.Services.AddScoped<IContentFileMetadataRepository, MongoContentFileMetadataRepository>();
builder.Services.AddScoped<ITagGroupRepository, MongoTagGroupRepository>();
builder.Services.AddScoped<IContentStatusRepository, MongoContentStatusRepository>();
builder.Services.AddScoped<ICategoryRepository, MongoCategoryRepository>();
builder.Services.AddScoped<ITagRepository, MongoTagRepository>();
builder.Services.AddScoped<IContentVersionRepository, MongoContentVersionRepository>();
builder.Services.AddScoped<IContentQueries, MongoContentQueries>();
builder.Services.AddSingleton<RevEx.Application.Subscriptions.IContentSubscriptions, Subscriptions>();

builder.Services.AddSingleton<IFileStorage, InMemoryFileStorage>();
builder.Services.AddSingleton<IContentHistoryStore, History>();
builder.Services.AddAuthentication("Checks").AddScheme<AuthenticationSchemeOptions, ChecksAuthentication>("Checks", _ => { });
builder.Services.AddAuthorization(o => { o.AddPolicy("EditContent", p => p.RequireAuthenticatedUser()); o.AddPolicy("AdministerCatalog", p => p.RequireAuthenticatedUser()); });
await using var app = builder.Build();
app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();

try
{
    await VersionEnumChecks.RunMigrationAsync(app.Services, database);
    await FileMetadataChecks.RunAsync(app.Services, database);
    using (var scope = app.Services.CreateScope())
    {
        await scope.ServiceProvider.GetRequiredService<ICategoryRepository>().AddAsync(new Category { Name = "Checks" }, default);
        await scope.ServiceProvider.GetRequiredService<IContentStatusRepository>().AddAsync(new ContentStatus { Name = "Draft" }, default);
    }
    using (var scope = app.Services.CreateScope())
    {
        var provider = scope.ServiceProvider;
        var tags = provider.GetRequiredService<ITagRepository>();
        var versions = provider.GetRequiredService<IContentVersionRepository>();
        var files = provider.GetRequiredService<IContentFileRepository>();
        var contents = provider.GetRequiredService<IContentRepository>();
        var groups = provider.GetRequiredService<ITagGroupRepository>();
        var loadedGroup = await groups.AddAsync(new TagGroup { Name = "Linked group" }, default);
        var tag = await tags.AddAsync(new RevEx.Domain.Entities.Tag { Name = "EF tag", TagGroupId = loadedGroup.Id }, default);
        var item = await contents.AddAsync(new Content { Cid = Uuid7.Create(), Name = "Cascade", Description = "Checks", CategoryId = 1, TagIds = [tag.Id] }, default);
        var filtered = await provider.GetRequiredService<IContentQueries>().QueryAsync(new ContentQueryDto { TagIds = [tag.Id] }, default);
        if (filtered.Count != 1) throw new Exception("Tag filter failed");
        var contentList = await contents.GetAllAsync(default);
        if (contentList.Single(x => x.Id == item.Id).Tags.Single().Id != tag.Id)
            throw new Exception("Content list tag loading failed");
        if ((await contents.GetByIdAsync(item.Id, default))!.Tags.Single().Id != tag.Id)
            throw new Exception("Single content tag loading failed");
        if ((await tags.GetAllAsync(default)).Single(x => x.Id == tag.Id).TagGroup?.Id != loadedGroup.Id)
            throw new Exception("Tag list group loading failed");
        if ((await tags.GetByIdAsync(tag.Id, default))!.TagGroup?.Id != loadedGroup.Id)
            throw new Exception("Single tag group loading failed");
        await provider.GetRequiredService<RevEx.Application.Catalog.TagsService>().Delete(tag.Id, default);
        if ((await contents.GetByIdAsync(item.Id, default))!.TagIds.Count != 0) throw new Exception("Tag cleanup failed");
        var seededVersion = await versions.AddAsync(new ContentVersion
        {
            ContentId = item.Id,
            Name = "v1",
            Status = "draft",
            PublishStatus = PublishStatus.Draft,
            Platform = Platform.Revit,
            ApplicationVersion = "2026"
        }, default);
        await files.AddAsync(new ContentFile { ContentVersionId = seededVersion.Id, Url = "test", OriginalFileName = "test", Extension = ".rfa", Role = "model" }, default);
        if ((await versions.GetAllAsync(default)).Single(x => x.Id == seededVersion.Id).Files.Count != 1)
            throw new Exception("Version list file loading failed");
        if ((await versions.GetByIdAsync(seededVersion.Id, default))!.Files.Count != 1)
            throw new Exception("Single version file loading failed");
        await provider.GetRequiredService<RevEx.Application.Content.ContentService>().Delete(item.Id, default);
        if ((await versions.GetAllAsync(default)).Count != 0 || (await files.GetAllAsync(default)).Count != 0) throw new Exception("Cascade delete failed");
    }
    ((History)app.Services.GetRequiredService<IContentHistoryStore>()).Items.Clear();
    await app.StartAsync();
    using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    async Task Expect(HttpResponseMessage response, HttpStatusCode status)
    { if (response.StatusCode != status) throw new Exception($"Expected {status}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}"); }
    await Expect(await client.PostAsJsonAsync("/api/categories", new { name = "" }), HttpStatusCode.BadRequest);
    var categoryResponse = await client.PostAsJsonAsync("/api/categories", new { name = "Extra" });
    await Expect(categoryResponse, HttpStatusCode.Created);
    var category = (await categoryResponse.Content.ReadFromJsonAsync<RevEx.Domain.Contracts.Category.CategoryDto>())!;
    await Expect(await client.PutAsJsonAsync($"/api/categories/{category.Id}", new { name = "Renamed" }), HttpStatusCode.NoContent);
    await Expect(await client.DeleteAsync($"/api/categories/{category.Id}"), HttpStatusCode.NoContent);
    await Expect(await client.DeleteAsync("/api/categories/1"), HttpStatusCode.Conflict);
    var groupResponse = await client.PostAsJsonAsync("/api/tag-groups", new { name = "Group" });
    await Expect(groupResponse, HttpStatusCode.Created);
    var group = (await groupResponse.Content.ReadFromJsonAsync<RevEx.Domain.Contracts.Tag.TagGroupDto>())!;
    var tagResponse = await client.PostAsJsonAsync("/api/tags", new { name = "Tag", tagGroupId = group.Id });
    await Expect(tagResponse, HttpStatusCode.Created);
    var tagDto = (await tagResponse.Content.ReadFromJsonAsync<RevEx.Domain.Contracts.Tag.TagDto>())!;
    await Expect(await client.DeleteAsync($"/api/tag-groups/{group.Id}"), HttpStatusCode.Conflict);
    await Expect(await client.DeleteAsync($"/api/tags/{tagDto.Id}"), HttpStatusCode.NoContent);
    await Expect(await client.DeleteAsync($"/api/tag-groups/{group.Id}"), HttpStatusCode.NoContent);
    var first = await client.PostAsJsonAsync("/api/content", new CreateContentDto { Cid = Uuid7.Create(), Name = "Alpha [test]", Description = "First", CategoryId = 1 });
    await Expect(first, HttpStatusCode.Created);
    var content = (await first.Content.ReadFromJsonAsync<ContentDto>())!;
    await Expect(
        await client.PostAsJsonAsync(
            "/api/content",
            new CreateContentDto
            {
                Cid = Guid.Empty,
                Name = "Invalid CID",
                Description = "Invalid",
                CategoryId = 1
            }),
        HttpStatusCode.BadRequest);
    await Expect(
        await client.PostAsJsonAsync(
            "/api/content",
            new CreateContentDto
            {
                Cid = content.Cid,
                Name = "Duplicate CID",
                Description = "Invalid",
                CategoryId = 1
            }),
        HttpStatusCode.Conflict);
    var foundByCid = await client.GetFromJsonAsync<ContentDto[]>(
        "/api/content/query?Cid=" + content.Cid.ToString("D"));
    if (foundByCid is null || foundByCid.Length != 1 || foundByCid[0].Id != content.Id)
    {
        throw new Exception("Exact CID lookup failed.");
    }

    var uuidResponse = await client.PostAsync("/api/identifiers/uuid-v7", null);
    await Expect(uuidResponse, HttpStatusCode.OK);
    var identifiedVid = await uuidResponse.Content.ReadFromJsonAsync<Guid>();
    if (!Uuid7.IsValid(identifiedVid))
    {
        throw new Exception("The server did not return a UUID v7.");
    }
    var identifiedVersionRequest = new RevEx.Domain.Contracts.ContentVersion.CreateContentVersionDto
    {
        ContentId = content.Id,
        Vid = identifiedVid,
        Platform = Platform.Revit,
        ApplicationVersion = "2026"
    };
    await Expect(
        await client.PostAsJsonAsync("/api/content-versions", identifiedVersionRequest),
        HttpStatusCode.BadRequest);
    identifiedVersionRequest = new RevEx.Domain.Contracts.ContentVersion.CreateContentVersionDto
    {
        ContentId = content.Id,
        Cid = Uuid7.Create(),
        Vid = identifiedVid,
        Platform = Platform.Revit,
        ApplicationVersion = "2026"
    };
    await Expect(
        await client.PostAsJsonAsync("/api/content-versions", identifiedVersionRequest),
        HttpStatusCode.Conflict);
    identifiedVersionRequest = new RevEx.Domain.Contracts.ContentVersion.CreateContentVersionDto
    {
        ContentId = content.Id,
        Cid = content.Cid,
        Vid = identifiedVid,
        Platform = Platform.Revit,
        ApplicationVersion = "2026"
    };
    var identifiedVersionResponse = await client.PostAsJsonAsync(
        "/api/content-versions",
        identifiedVersionRequest);
    await Expect(identifiedVersionResponse, HttpStatusCode.Created);
    var identifiedVersion = await identifiedVersionResponse.Content.ReadFromJsonAsync<RevEx.Domain.Contracts.ContentVersion.ContentVersionDto>();
    var identifiedVersionRetry = await client.PostAsJsonAsync(
        "/api/content-versions",
        identifiedVersionRequest);
    await Expect(identifiedVersionRetry, HttpStatusCode.Created);
    var retriedIdentifiedVersion = await identifiedVersionRetry.Content.ReadFromJsonAsync<RevEx.Domain.Contracts.ContentVersion.ContentVersionDto>();
    if (identifiedVersion is null ||
        retriedIdentifiedVersion is null ||
        identifiedVersion.Id != retriedIdentifiedVersion.Id)
    {
        throw new Exception("VID retry created a duplicate version.");
    }

    await VersionEnumChecks.RunHttpAsync(client, content.Id);
    using (var missingIdentifiers = new MultipartFormDataContent())
    {
        missingIdentifiers.Add(new ByteArrayContent([1, 2, 3]), "File", "family.rfa");
        missingIdentifiers.Add(new StringContent("primary"), "Role");
        var fileRoute = $"/api/content-versions/{identifiedVersion.Id}/file";
        await Expect(await client.PostAsync(fileRoute, missingIdentifiers), HttpStatusCode.BadRequest);
        var identifiers = System.Text.Json.JsonSerializer.Serialize(new
        {
            CID = new { Value = content.Cid },
            VID = new { Value = identifiedVid }
        });
        using var preparedFile = new MultipartFormDataContent();
        preparedFile.Add(new ByteArrayContent([1, 2, 3]), "File", "family.rfa");
        preparedFile.Add(new StringContent("primary"), "Role");
        preparedFile.Add(new StringContent(identifiers), "MetadataJson");
        await Expect(await client.PostAsync(fileRoute, preparedFile), HttpStatusCode.Created);
    }
    await Expect(await client.PostAsJsonAsync("/api/content", new CreateContentDto { Cid = Uuid7.Create(), Name = "Beta", Description = "Second", CategoryId = 1 }), HttpStatusCode.Created);
    var found = (await client.GetFromJsonAsync<ContentDto[]>("/api/content/query?Name=%5Btest%5D&Limit=1"))!;
    if (found.Length != 1 || found[0].Id != content.Id) throw new Exception("Mongo literal name search failed");
    var page = (await client.GetFromJsonAsync<ContentDto[]>("/api/content/query?Offset=1&Limit=1&CategoryIds=1"))!;
    if (page.Length != 1 || page[0].Name != "Beta") throw new Exception("Mongo pagination failed");
    await Expect(await client.GetAsync("/api/content/query?Limit=0"), HttpStatusCode.BadRequest);
    await Expect(await client.GetAsync("/api/content/999999"), HttpStatusCode.NotFound);
    await Expect(await client.PutAsJsonAsync($"/api/content/{content.Id}", new UpdateContentDto { Name = "Updated", Description = "Updated", CategoryId = 1 }), HttpStatusCode.NoContent);
    using var upload = new MultipartFormDataContent();
    var image = new ByteArrayContent([1,2,3]); image.Headers.ContentType = new("image/png"); upload.Add(image, "File", "test.png");
    await Expect(await client.PostAsync($"/api/content/{content.Id}/preview", upload), HttpStatusCode.OK);
    await Expect(await client.DeleteAsync($"/api/content/{content.Id}/preview"), HttpStatusCode.NoContent);
    var versionResponse = await client.PostAsJsonAsync("/api/content-versions", new { contentId = content.Id, name = "v1", platform = Platform.Revit, application = "revit", applicationVersion = "2026" });
    await Expect(versionResponse, HttpStatusCode.Created);
    var version = (await versionResponse.Content.ReadFromJsonAsync<RevEx.Domain.Contracts.ContentVersion.ContentVersionDto>())!;
    using var fileUpload = new MultipartFormDataContent();
    fileUpload.Add(new ByteArrayContent([1,2,3]), "File", "family.rfa");
    fileUpload.Add(new StringContent("primary"), "Role");
    const string familyMetadata = """{"schema":"revit.test.v2","types":[{"name":"Тип А","parameters":{"width":900,"enabled":true,"optional":null}}],"future":{"items":[1,"two",{"nested":3.5}]}}""";
    fileUpload.Add(new StringContent(familyMetadata), "MetadataJson");
    await Expect(await client.PostAsync($"/api/content-versions/{version.Id}/file", fileUpload), HttpStatusCode.Created);
    var fetchedVersion = (await client.GetFromJsonAsync<RevEx.Domain.Contracts.ContentVersion.ContentVersionDto>($"/api/content-versions/{version.Id}"))!;
    await Expect(await client.GetAsync($"/api/content-files/{fetchedVersion.FileIds.Single()}/content"), HttpStatusCode.OK);
    var fileId = fetchedVersion.FileIds.Single();
    using (var metadataResponse = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync($"/api/content-files/{fileId}")))
    {
        var metadata = metadataResponse.RootElement.GetProperty("metadata");
        if (metadata.GetProperty("types")[0].GetProperty("parameters").GetProperty("width").GetInt32() != 900 ||
            metadata.GetProperty("future").GetProperty("items")[2].GetProperty("nested").GetDouble() != 3.5)
        {
            throw new Exception("Untyped metadata did not survive the HTTP/Mongo roundtrip.");
        }
    }
    var mongo = new MongoDB.Driver.MongoClient("mongodb://localhost:27017").GetDatabase(database);
    var rawFiles = mongo.GetCollection<MongoDB.Bson.BsonDocument>("contentFiles");
    var rawFile = await MongoDB.Driver.IAsyncCursorSourceExtensions.FirstOrDefaultAsync(
        MongoDB.Driver.IMongoCollectionExtensions.Find(rawFiles, MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", fileId)));
    if (rawFile.Contains("Metadata") || rawFile.Contains("MetadataJson"))
    {
        throw new Exception("File records must not contain metadata after extraction into a separate collection.");
    }
    var rawMetadataCollection = mongo.GetCollection<MongoDB.Bson.BsonDocument>("contentFileMetadata");
    var rawMetadata = await MongoDB.Driver.IAsyncCursorSourceExtensions.FirstOrDefaultAsync(
        MongoDB.Driver.IMongoCollectionExtensions.Find(rawMetadataCollection,
            MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("FileId", fileId)));
    if (rawMetadata is null || !rawMetadata["Metadata"].IsBsonDocument || rawMetadata.Contains("MetadataJson"))
    {
        throw new Exception("Metadata must be a separate BSON document linked by FileId.");
    }

    var drawingResponse = await client.PostAsJsonAsync("/api/content-versions", new { contentId = content.Id, name = "DWG", platform = Platform.AutoCad, application = "autocad", applicationVersion = "R24.3" });
    await Expect(drawingResponse, HttpStatusCode.Created);
    var drawingVersion = (await drawingResponse.Content.ReadFromJsonAsync<RevEx.Domain.Contracts.ContentVersion.ContentVersionDto>())!;
    using var invalidMetadataUpload = new MultipartFormDataContent();
    invalidMetadataUpload.Add(new ByteArrayContent([4, 5, 6]), "File", "drawing.dwg");
    invalidMetadataUpload.Add(new StringContent("primary"), "Role");
    invalidMetadataUpload.Add(new StringContent("[1,2]"), "MetadataJson");
    await Expect(await client.PostAsync($"/api/content-versions/{drawingVersion.Id}/file", invalidMetadataUpload), HttpStatusCode.BadRequest);
    var emptyDrawing = (await client.GetFromJsonAsync<RevEx.Domain.Contracts.ContentVersion.ContentVersionDto>($"/api/content-versions/{drawingVersion.Id}"))!;
    if (emptyDrawing.FileIds.Count != 0)
    {
        throw new Exception("Invalid metadata left a file record behind.");
    }
    using var drawingUpload = new MultipartFormDataContent();
    drawingUpload.Add(new ByteArrayContent([4, 5, 6]), "File", "drawing.dwg");
    drawingUpload.Add(new StringContent("primary"), "Role");
    drawingUpload.Add(new StringContent("""{"drawing":{"layers":["Walls","Doors"],"units":"mm"}}"""), "MetadataJson");
    await Expect(await client.PostAsync($"/api/content-versions/{drawingVersion.Id}/file", drawingUpload), HttpStatusCode.Created);
    using (var scope = app.Services.CreateScope())
    {
        var filesBeforeFailure = await rawFiles.CountDocumentsAsync(MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Empty);
        var metadataBeforeFailure = await rawMetadataCollection.CountDocumentsAsync(MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Empty);
        var failedUpload = new RevEx.Application.Catalog.VersionFileUpload(
            "failure.rfa", 3, () => throw new IOException("Simulated stream failure."), "attachment", familyMetadata);
        var failed = false;
        try
        {
            await scope.ServiceProvider.GetRequiredService<RevEx.Application.Catalog.ContentVersionsService>()
                .AddFile(drawingVersion.Id, failedUpload, default);
        }
        catch (IOException)
        {
            failed = true;
        }

        if (!failed
            || await rawFiles.CountDocumentsAsync(MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Empty) != filesBeforeFailure
            || await rawMetadataCollection.CountDocumentsAsync(MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Empty) != metadataBeforeFailure)
        {
            throw new Exception("Failed file upload left a file record or separate metadata document behind.");
        }
    }

    await Expect(await client.DeleteAsync($"/api/content-versions/{version.Id}"), HttpStatusCode.NoContent);
    await Expect(await client.DeleteAsync($"/api/content/{content.Id}"), HttpStatusCode.NoContent);
    var remainingMetadata = await rawMetadataCollection.CountDocumentsAsync(
        MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Empty);
    if (remainingMetadata != 0)
    {
        throw new Exception("Deleting content or a version left orphan metadata documents.");
    }
    if (app.Services.GetRequiredService<IContentHistoryStore>() is not History history || history.Items.Any(x => x.Author != "checks")) throw new Exception("History author failed");
    Console.WriteLine("PASS: HTTP create/update/delete/preview, error mapping, current user, real Mongo search and pagination.");
}
finally { await app.StopAsync(); await new MongoDB.Driver.MongoClient("mongodb://localhost:27017").DropDatabaseAsync(database); }
