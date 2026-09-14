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

    public static async Task SeedAsync(
        AppDbContext dbContext,
        IFileStorage fileStorage,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

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
                Status = "draft"
            };
            dbContext.ContentVersions.Add(version);
        }

        var file = version.Files.FirstOrDefault();
        if (file is null)
        {
            file = new ContentFile
            {
                Url = "pending",
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

        var bytes = Encoding.UTF8.GetBytes(
            "RevEx\n\nЭто стартовый файл для версии 1.0.0.\n");
        await using var stream = new MemoryStream(bytes, writable: false);
        await fileStorage.SaveAsync(file.Id, stream, cancellationToken);
    }
}
