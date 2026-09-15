using System.Text;
using Microsoft.EntityFrameworkCore;
using RevEx.Application.Abstractions.Storage;
using RevEx.Domain.Entities;
using ContentEntity = RevEx.Domain.Entities.Content;

namespace RevEx.Infrastructure.Data;

public static class InitialDataSeeder
{
    private const string DefaultCategoryName = "Общие";
    private const string DefaultContentName = "Добро пожаловать в RevEx";
    private const string DoorContentName = "Дверь с остеклением и нажимной пластиной";
    private const string DoorFileName = "Door-Single-Glass-Push-Plate.rfa";
    private const string DoorResourceName = "RevEx.SeedFiles.Door-Single-Glass-Push-Plate.rfa";

    public static async Task SeedAsync(
        AppDbContext dbContext,
        IFileStorage fileStorage,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var bytes = Encoding.UTF8.GetBytes(
            "RevEx\n\nЭто стартовый файл для версии 1.0.0.\n");

        var category = await dbContext.Categories
            .FirstOrDefaultAsync(item => item.Name == DefaultCategoryName, cancellationToken);

        if (category is null)
        {
            category = new Category { Name = DefaultCategoryName };
            dbContext.Categories.Add(category);
        }

        var status = await dbContext.ContentStatuses
            .FirstOrDefaultAsync(item => item.Name == "draft", cancellationToken);

        if (status is null)
        {
            status = new ContentStatus { Name = "draft" };
            dbContext.ContentStatuses.Add(status);
        }

        var content = await dbContext.Contents
            .FirstOrDefaultAsync(
                item => item.CategoryId == category.Id && item.Name == DefaultContentName,
                cancellationToken);

        if (content is null)
        {
            content = new ContentEntity
            {
                Name = DefaultContentName,
                Description = "Стартовый контент, созданный автоматически при запуске приложения.",
                Category = category,
                ContentStatus = status
            };
            dbContext.Contents.Add(content);
        }

        var version = await dbContext.ContentVersions
            .Include(item => item.Files)
            .FirstOrDefaultAsync(
                item => item.ContentId == content.Id && item.Name == "1.0.0",
                cancellationToken);

        if (version is null)
        {
            version = new ContentVersion
            {
                Content = content,
                Name = "1.0.0",
                Date = DateTimeOffset.UtcNow,
                Status = "draft",
                Application = "revit",
                ApplicationVersion = "2026"
            };
            dbContext.ContentVersions.Add(version);
        }

        var file = version.Files.FirstOrDefault();
        if (file is null)
        {
            file = new ContentFile
            {
                Url = "pending",
                OriginalFileName = "RevEx-Welcome.txt",
                Extension = ".txt",
                SizeBytes = bytes.LongLength,
                Role = "attachment",
                ContentVersion = version
            };
            dbContext.Files.Add(file);
            await dbContext.SaveChangesAsync(cancellationToken);

            file.Url = $"/api/content-files/{file.Id}/content";
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await using var stream = new MemoryStream(bytes, writable: false);
        await fileStorage.SaveAsync(file.Id, stream, cancellationToken);

        await SeedDoorAsync(dbContext, fileStorage, category, status, cancellationToken);
    }

    private static async Task SeedDoorAsync(
        AppDbContext dbContext,
        IFileStorage fileStorage,
        Category category,
        ContentStatus status,
        CancellationToken cancellationToken)
    {
        var doorBytes = await ReadEmbeddedDoorAsync(cancellationToken);
        var content = await dbContext.Contents
            .FirstOrDefaultAsync(
                item => item.CategoryId == category.Id && item.Name == DoorContentName,
                cancellationToken);

        if (content is null)
        {
            content = new ContentEntity
            {
                Name = DoorContentName,
                Description =
                    "Демонстрационное семейство одностворчатой двери с большим остеклением " +
                    "и вертикальной нажимной пластиной.",
                Category = category,
                ContentStatus = status
            };
            dbContext.Contents.Add(content);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await SeedDoorVersionAsync(
            dbContext,
            fileStorage,
            content,
            doorBytes,
            "1.0.0",
            new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero),
            cancellationToken);
        await SeedDoorVersionAsync(
            dbContext,
            fileStorage,
            content,
            doorBytes,
            "2.0.0",
            new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            cancellationToken);
    }

    private static async Task SeedDoorVersionAsync(
        AppDbContext dbContext,
        IFileStorage fileStorage,
        ContentEntity content,
        byte[] doorBytes,
        string versionName,
        DateTimeOffset versionDate,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.ContentVersions
            .Include(item => item.Files)
            .FirstOrDefaultAsync(
                item => item.ContentId == content.Id && item.Name == versionName,
                cancellationToken);

        if (version is null)
        {
            version = new ContentVersion
            {
                Content = content,
                Name = versionName,
                Date = versionDate,
                Status = "published",
                Application = "revit",
                ApplicationVersion = "2011"
            };
            dbContext.ContentVersions.Add(version);
        }

        var file = version.Files.FirstOrDefault(item => item.Role == "primary");
        if (file is null)
        {
            file = new ContentFile
            {
                Url = "pending",
                OriginalFileName = DoorFileName,
                Extension = ".rfa",
                SizeBytes = doorBytes.LongLength,
                Role = "primary",
                ContentVersion = version
            };
            dbContext.Files.Add(file);
            await dbContext.SaveChangesAsync(cancellationToken);

            file.Url = $"/api/content-files/{file.Id}/content";
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await using var stream = new MemoryStream(doorBytes, writable: false);
        await fileStorage.SaveAsync(file.Id, stream, cancellationToken);
    }

    private static async Task<byte[]> ReadEmbeddedDoorAsync(CancellationToken cancellationToken)
    {
        await using var source = typeof(InitialDataSeeder).Assembly
            .GetManifestResourceStream(DoorResourceName)
            ?? throw new InvalidOperationException($"Embedded seed file '{DoorResourceName}' was not found.");
        await using var destination = new MemoryStream();
        await source.CopyToAsync(destination, cancellationToken);
        return destination.ToArray();
    }
}
