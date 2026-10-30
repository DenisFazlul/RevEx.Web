using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using RevEx.Api.Controllers;
using RevEx.Api.Extensions;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;
using RevEx.Infrastructure.Repositories;
using RevEx.Infrastructure.Storage;

var database = "revex_api_checks_" + Guid.NewGuid().ToString("N");
var builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Logging.ClearProviders();
builder.Services.AddApiControllers().AddControllers().AddApplicationPart(typeof(ContentController).Assembly);
builder.Services.AddSingleton<IOptions<MongoDbOptions>>(Options.Create(new MongoDbOptions { ConnectionString = "mongodb://localhost:27017", DatabaseName = database }));
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(MongoRepository<>));
builder.Services.AddScoped<IContentQueries, MongoContentQueries>();
builder.Services.AddScoped<IUnitOfWork, MongoUnitOfWork>();
builder.Services.AddSingleton<IFileStorage, InMemoryFileStorage>();
builder.Services.AddSingleton<IContentHistoryStore, History>();
builder.Services.AddAuthentication("Checks").AddScheme<AuthenticationSchemeOptions, ChecksAuthentication>("Checks", _ => { });
builder.Services.AddAuthorization(o => o.AddPolicy("EditContent", p => p.RequireAuthenticatedUser()));
await using var app = builder.Build();
app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
var context = app.Services.GetRequiredService<MongoDbContext>();
try
{
    using (var scope = app.Services.CreateScope())
    {
        await scope.ServiceProvider.GetRequiredService<IRepository<Category>>().AddAsync(new Category { Name = "Checks" }, default);
        await scope.ServiceProvider.GetRequiredService<IRepository<ContentStatus>>().AddAsync(new ContentStatus { Name = "Draft" }, default);
    }
    await app.StartAsync();
    using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    async Task Expect(HttpResponseMessage response, HttpStatusCode status)
    { if (response.StatusCode != status) throw new Exception($"Expected {status}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}"); }
    var first = await client.PostAsJsonAsync("/api/content", new CreateContentDto { Name = "Alpha [test]", Description = "First", CategoryId = 1 });
    await Expect(first, HttpStatusCode.Created);
    var content = (await first.Content.ReadFromJsonAsync<ContentDto>())!;
    await Expect(await client.PostAsJsonAsync("/api/content", new CreateContentDto { Name = "Beta", Description = "Second", CategoryId = 1 }), HttpStatusCode.Created);
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
    await Expect(await client.DeleteAsync($"/api/content/{content.Id}"), HttpStatusCode.NoContent);
    if (app.Services.GetRequiredService<IContentHistoryStore>() is not History history || history.Items.Any(x => x.Author != "checks")) throw new Exception("History author failed");
    Console.WriteLine("PASS: HTTP create/update/delete/preview, error mapping, current user, real Mongo search and pagination.");
}
finally { await app.StopAsync(); await context.Database.Client.DropDatabaseAsync(database); }

