using RevEx.Application.Content;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.Abstractions.Identity;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Domain.Entities;

static void Check(bool condition) { if (!condition) throw new Exception("Check failed"); }
static async Task Failure(Func<Task> action, ContentFailure kind)
{
    try { await action(); } catch (ContentOperationException e) when (e.Failure == kind) { return; }
    throw new Exception("Expected " + kind);
}
var contents = new Repository<Content>();
var categories = new Repository<Category>();
var statuses = new Repository<ContentStatus>();
var tags = new Repository<Tag>();
var files = new Repository<ContentFile>();
var history = new History();
var storage = new Storage();
var queries = new Queries();
var service = new ContentService(contents, categories, statuses, tags, files,
    new Repository<ContentVersion>(), history, storage, new UnitOfWork(), queries, new User());
await Failure(() => service.Create(new(), default), ContentFailure.Validation);
await Failure(() => service.Create(new() { Name = "Name", Description = "Description", CategoryId = 1 }, default), ContentFailure.NotFound);
Check(contents.Items.Count == 0 && history.Items.Count == 0);
categories.Items.Add(new Category { Id = 1, Name = "Category" });
statuses.Items.Add(new ContentStatus { Id = 1, Name = "Draft" });
tags.Items.Add(new Tag { Id = 1, Name = "Tag" });
var created = await service.Create(new() { Name = " Name ", Description = " Description ", CategoryId = 1, TagIds = [1,1] }, default);
Check(created.Name == "Name" && created.Description == "Description" && created.TagIds.Count == 1);
Check(history.Items.Single().Author == "tester");
await Failure(() => service.Query(new() { Offset = -1 }, default), ContentFailure.Validation);
await Failure(() => service.Query(new() { Limit = 0 }, default), ContentFailure.Validation);
var query = new ContentQueryDto { Name = "Name", Offset = 2, Limit = 3 };
await service.Query(query, default);
Check(ReferenceEquals(queries.Last, query));
await Failure(() => service.UploadPreview(created.Id, new("x.exe", "image/png", 10, () => new MemoryStream()), default), ContentFailure.BadRequest);
Check(files.Items.Count == 0);
storage.Fail = true;
try { await service.UploadPreview(created.Id, new("x.png", "image/png", 10, () => new MemoryStream()), default); throw new Exception("Expected storage failure"); }
catch (IOException) { }
Check(files.Items.Count == 0 && storage.Deleted.Count == 1 && contents.Items.Single().PreviewFileId == null);
await Failure(() => service.GetById(999, default), ContentFailure.NotFound);
Console.WriteLine("Application checks passed: validation, creation, history author, query delegation, preview cleanup.");
