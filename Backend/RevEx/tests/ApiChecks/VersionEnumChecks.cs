using System.Net;
using System.Net.Http.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.ContentVersion;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;
using RevEx.Infrastructure.Persistence.Migrations;

internal static class VersionEnumChecks
{
    public static async Task RunMigrationAsync(IServiceProvider services, string database)
    {
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IContentVersionRepository>();
        var context = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        var raw = new MongoClient("mongodb://localhost:27017").GetDatabase(database).GetCollection<BsonDocument>("contentVersions");
        var fixtures = new[]
        {
            (Application: " ReViT ", Status: "draft", Platform: Platform.Revit, PublishStatus: PublishStatus.Draft),
            (Application: "AUTOCAD", Status: " Published ", Platform: Platform.AutoCad, PublishStatus: PublishStatus.Ready),
            (Application: "other-cad", Status: "ARCHIVED", Platform: Platform.Other, PublishStatus: PublishStatus.Withdrawn)
        };
        var ids = new List<int>();
        foreach (var fixture in fixtures)
        {
            var version = await repository.AddAsync(new ContentVersion
            {
                Name = "Legacy",
                ContentId = -1,
                Date = DateTimeOffset.UtcNow,
                Application = fixture.Application,
                ApplicationVersion = "2026",
                Status = fixture.Status,
                Platform = Platform.Other,
                PublishStatus = PublishStatus.Draft
            }, default);
            ids.Add(version.Id);
            var legacyFields = ids.Count == 1
                ? Builders<BsonDocument>.Update.Set("Platform", BsonNull.Value).Set("PublishStatus", BsonNull.Value)
                : Builders<BsonDocument>.Update.Unset("Platform").Unset("PublishStatus");
            await raw.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", version.Id), legacyFields);
        }

        var assigned = await repository.AddAsync(new ContentVersion
        {
            Name = "Already assigned",
            ContentId = -1,
            Date = DateTimeOffset.UtcNow,
            Application = "autocad",
            ApplicationVersion = "2026",
            Status = "draft",
            Platform = Platform.Revit,
            PublishStatus = PublishStatus.Ready
        }, default);
        await ContentVersionEnumsMigration.RunAsync(context);
        await ContentVersionEnumsMigration.RunAsync(context);
        for (var index = 0; index < fixtures.Length; index++)
        {
            var restored = await repository.GetByIdAsync(ids[index], default);
            Require(restored?.Platform == fixtures[index].Platform && restored.PublishStatus == fixtures[index].PublishStatus,
                "Legacy version enums were not migrated correctly.");
            var document = await raw.Find(Builders<BsonDocument>.Filter.Eq("_id", ids[index])).SingleAsync();
            Require(document["Platform"].IsInt32 && document["PublishStatus"].IsInt32, "Enums must be stored as integers.");
        }

        var preserved = await repository.GetByIdAsync(assigned.Id, default);
        Require(preserved?.Platform == Platform.Revit && preserved.PublishStatus == PublishStatus.Ready,
            "Migration overwrote explicitly assigned enums.");
        ids.Add(assigned.Id);
        await raw.DeleteManyAsync(Builders<BsonDocument>.Filter.In("_id", ids));
        Console.WriteLine("PASS: version platform/status migration, enum persistence and repeated migration.");
    }

    public static async Task RunHttpAsync(HttpClient client, int contentId)
    {
        await ExpectAsync(await client.PostAsJsonAsync("/api/content-versions",
            new { contentId, applicationVersion = "2026" }), HttpStatusCode.BadRequest);
        await ExpectAsync(await client.PostAsJsonAsync("/api/content-versions",
            new { contentId, platform = 999, applicationVersion = "2026" }), HttpStatusCode.BadRequest);
        await ExpectAsync(await client.PostAsJsonAsync("/api/content-versions",
            new { contentId, platform = Platform.Revit, publishStatus = 999, applicationVersion = "2026" }), HttpStatusCode.BadRequest);
        using var response = await client.PostAsJsonAsync("/api/content-versions", new CreateContentVersionDto
        {
            ContentId = contentId,
            Name = "Typed version",
            Platform = Platform.AutoCad,
            PublishStatus = PublishStatus.Draft,
            ApplicationVersion = "R24.3"
        });
        await ExpectAsync(response, HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ContentVersionDto>()
            ?? throw new InvalidOperationException("Version response is empty.");
        Require(created.Platform == Platform.AutoCad && created.Application == "autocad", "Platform did not determine the application.");
        var route = $"/api/content-versions/{created.Id}/publish-status";
        await ExpectAsync(await client.PutAsJsonAsync(route, new { publishStatus = 999 }), HttpStatusCode.BadRequest);
        await ExpectAsync(await client.PutAsJsonAsync(route, new { }), HttpStatusCode.BadRequest);
        await ExpectAsync(await client.PutAsJsonAsync("/api/content-versions/999999/publish-status",
            new { publishStatus = PublishStatus.Ready }), HttpStatusCode.NotFound);
        foreach (var status in new[] { PublishStatus.Ready, PublishStatus.Withdrawn, PublishStatus.Draft })
        {
            await ExpectAsync(await client.PutAsJsonAsync(route, new { publishStatus = status }), HttpStatusCode.NoContent);
            var restored = await client.GetFromJsonAsync<ContentVersionDto>($"/api/content-versions/{created.Id}")
                ?? throw new InvalidOperationException("Version disappeared.");
            Require(restored.PublishStatus == status && restored.Platform == created.Platform && restored.Date == created.Date
                && restored.Name == created.Name && restored.ApplicationVersion == created.ApplicationVersion,
                "Changing the publish status changed other version fields.");
        }

        await ExpectAsync(await client.PutAsJsonAsync($"/api/content-versions/{created.Id}", new UpdateContentVersionDto
        {
            Name = created.Name,
            Date = created.Date,
            PublishStatus = PublishStatus.Ready,
            Platform = Platform.Revit,
            ApplicationVersion = "2026"
        }), HttpStatusCode.NoContent);
        var updated = await client.GetFromJsonAsync<ContentVersionDto>($"/api/content-versions/{created.Id}");
        Require(updated?.Platform == Platform.Revit && updated.PublishStatus == PublishStatus.Ready && updated.Status == "published",
            "Full version update did not persist enums.");
        var listed = await client.GetFromJsonAsync<ContentVersionDto[]>("/api/content-versions");
        Require(listed?.Single(version => version.Id == created.Id).Platform == Platform.Revit, "Version list lost the platform.");
        await ExpectAsync(await client.DeleteAsync($"/api/content-versions/{created.Id}"), HttpStatusCode.NoContent);
        Console.WriteLine("PASS: required platform, enum validation, all publication statuses, partial update and HTTP/Mongo roundtrip.");
    }

    private static async Task ExpectAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        if (response.StatusCode != status)
        {
            throw new InvalidOperationException($"Expected {status}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
