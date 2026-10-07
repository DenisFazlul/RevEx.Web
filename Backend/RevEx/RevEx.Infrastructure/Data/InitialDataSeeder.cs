using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Domain.Entities;
using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Infrastructure.Data;

public static class InitialDataSeeder
{
    public static async Task SeedAsync(
        IRepository<Category> categories,
        IRepository<ContentStatus> statuses,
        IRepository<TagGroup> tagGroups,
        IRepository<ContentEntity> contents,
        IRepository<ContentVersion> versions,
        IRepository<ContentFile> files,
        IFileStorage fileStorage,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken = default)
    {
        var category = (await categories.GetAllAsync(cancellationToken)).FirstOrDefault(x => x.Name == "Общие");
        if (category is null)
            category = await categories.AddAsync(new Category { Name = "Общие" }, cancellationToken);

        var status = (await statuses.GetAllAsync(cancellationToken)).FirstOrDefault(x => x.Name == "draft");
        if (status is null)
            status = await statuses.AddAsync(new ContentStatus { Name = "draft" }, cancellationToken);

        if (!(await tagGroups.GetAllAsync(cancellationToken)).Any(x => x.Name == "Общие"))
            await tagGroups.AddAsync(new TagGroup { Name = "Общие" }, cancellationToken);

        if (!(await contents.GetAllAsync(cancellationToken)).Any(x => x.Name == "Добро пожаловать в RevEx"))
        {
            await contents.AddAsync(new ContentEntity
            {
                Name = "Добро пожаловать в RevEx",
                Description = "Стартовый контент, созданный автоматически при запуске приложения.",
                CategoryId = category.Id,
                Category = category,
                ContentStatusId = status.Id,
                ContentStatus = status
            }, cancellationToken);
        }

        const string doorName = "Дверь с остеклением и нажимной пластиной";
        var door = (await contents.GetAllAsync(cancellationToken)).FirstOrDefault(x => x.Name == doorName);
        if (door is null)
        {
            door = await contents.AddAsync(new ContentEntity
            {
                Name = doorName,
                Description = "Демонстрационное семейство одностворчатой двери с большим остеклением и вертикальной нажимной пластиной.",
                CategoryId = category.Id,
                Category = category,
                ContentStatusId = status.Id,
                ContentStatus = status
            }, cancellationToken);
        }

        var doorBytes = await ReadEmbeddedDoorAsync(cancellationToken);
        await SeedDoorVersionAsync(door.Id, "1.0.0", new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero),
            doorBytes, versions, files, fileStorage, cancellationToken);
        await SeedDoorVersionAsync(door.Id, "2.0.0", new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            doorBytes, versions, files, fileStorage, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDoorVersionAsync(
        int contentId,
        string name,
        DateTimeOffset date,
        byte[] bytes,
        IRepository<ContentVersion> versions,
        IRepository<ContentFile> files,
        IFileStorage fileStorage,
        CancellationToken cancellationToken)
    {
        var version = (await versions.GetAllAsync(cancellationToken))
            .FirstOrDefault(x => x.ContentId == contentId && x.Name == name);
        if (version is null)
        {
            version = await versions.AddAsync(new ContentVersion
            {
                ContentId = contentId,
                Name = name,
                Date = date,
                Status = "published",
                Application = "revit",
                ApplicationVersion = "2011"
            }, cancellationToken);
        }

        var file = (await files.GetAllAsync(cancellationToken))
            .FirstOrDefault(x => x.ContentVersionId == version.Id && x.Role == "primary");
        if (file is null)
        {
            file = await files.AddAsync(new ContentFile
            {
                Url = "pending",
                OriginalFileName = "Door-Single-Glass-Push-Plate.rfa",
                Extension = ".rfa",
                SizeBytes = bytes.LongLength,
                Role = "primary",
                ContentVersionId = version.Id
            }, cancellationToken);
            file.Url = $"/api/content-files/{file.Id}/content";
            await files.UpdateAsync(file, cancellationToken);
        }

        await using var stream = new MemoryStream(bytes, writable: false);
        await fileStorage.SaveAsync(file.Id, stream, cancellationToken);
    }

    private static async Task<byte[]> ReadEmbeddedDoorAsync(CancellationToken cancellationToken)
    {
        await using var source = typeof(InitialDataSeeder).Assembly
            .GetManifestResourceStream("RevEx.SeedFiles.Door-Single-Glass-Push-Plate.rfa")
            ?? throw new InvalidOperationException("Embedded seed Revit file was not found.");
        await using var destination = new MemoryStream();
        await source.CopyToAsync(destination, cancellationToken);
        return destination.ToArray();
    }
}
