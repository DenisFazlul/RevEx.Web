using RevEx.Application.Content;
using RevEx.Application.Catalog;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Application.Abstractions.Identity;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Domain.Contracts.ContentVersion;
using RevEx.Domain.Entities;
using RevEx.Domain.Identifiers;

static void Check(bool condition) { if (!condition) throw new Exception("Check failed"); }
static async Task Failure(Func<Task> action, ContentFailure kind)
{
    try { await action(); } catch (ContentOperationException e) when (e.Failure == kind) { return; }
    throw new Exception("Expected " + kind);
}
var contents = new ContentRepository();
var categories = new CategoryRepository();
var statuses = new ContentStatusRepository();
var tags = new TagRepository();
var files = new ContentFileRepository();
var history = new History();
var storage = new Storage();
var queries = new Queries();
var versions = new ContentVersionRepository();
var service = new ContentService(contents, categories, statuses, tags, files,
    versions, history, storage, queries, new User());
await Failure(() => service.Create(new(), default), ContentFailure.Validation);
await Failure(() => service.Create(new() { Cid = Uuid7.Create(), Name = "Name", Description = "Description", CategoryId = 1 }, default), ContentFailure.NotFound);
Check(contents.Items.Count == 0 && history.Items.Count == 0);
categories.Items.Add(new Category { Id = 1, Name = "Category" });
statuses.Items.Add(new ContentStatus { Id = 1, Name = "Draft" });
tags.Items.Add(new Tag { Id = 1, Name = "Tag" });
var created = await service.Create(new() { Name = " Name ", Description = " Description ", CategoryId = 1, TagIds = [1,1] }, default);
Check(Uuid7.IsValid(created.Cid));
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

var versionService = new ContentVersionsService(
    new User(),
    contents,
    versions,
    files,
    history,
    storage);
var vid = Uuid7.Create();
var versionRequest = new CreateContentVersionDto
{
    ContentId = created.Id,
    Platform = Platform.Revit,
    ApplicationVersion = "2026",
    Vid = vid
};
await Failure(() => versionService.Create(versionRequest, default), ContentFailure.Validation);
versionRequest = new CreateContentVersionDto
{
    ContentId = created.Id,
    Platform = Platform.Revit,
    ApplicationVersion = "2026",
    Cid = Uuid7.Create(),
    Vid = vid
};
await Failure(() => versionService.Create(versionRequest, default), ContentFailure.Conflict);
versionRequest = new CreateContentVersionDto
{
    ContentId = created.Id,
    Platform = Platform.Revit,
    ApplicationVersion = "2026",
    Cid = created.Cid,
    Vid = vid
};
var createdVersion = await versionService.Create(versionRequest, default);
var retriedVersion = await versionService.Create(versionRequest, default);
Check(createdVersion.Vid == vid && retriedVersion.Id == createdVersion.Id && versions.Items.Count == 1);

Console.WriteLine("Application checks passed: validation, creation, CID/VID matching, idempotency, history author, query delegation, preview cleanup.");
