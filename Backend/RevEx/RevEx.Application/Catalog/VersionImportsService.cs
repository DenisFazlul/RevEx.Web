using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Content;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentVersion;
using RevEx.Domain.Entities;
using RevEx.Domain.Identifiers;

namespace RevEx.Application.Catalog;

public sealed class VersionImportsService(
    IContentRepository contentRepository,
    IContentVersionRepository versionRepository,
    ContentService contentService,
    ContentVersionsService versionsService)
{
    public async Task<ContentVersionDto> Import(
        VersionImportRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var requestedFiles = new[] { request.PrimaryFile }
            .Concat(request.AuxiliaryFiles.Select(ToVersionFileUpload))
            .ToArray();
        foreach (var file in requestedFiles)
        {
            ContentVersionsService.ValidateUpload(file, request.Platform, request.Cid, request.Vid);
        }

        var content = await contentRepository.GetByCidAsync(request.Cid, cancellationToken);
        if (content is null)
        {
            if (request.CategoryId is null || string.IsNullOrWhiteSpace(request.Description))
            {
                throw new ContentOperationException(
                    ContentFailure.Validation,
                    "Description and CategoryId are required for new content.");
            }

            var createdContent = await contentService.Create(
                new CreateContentDto
                {
                    Cid = request.Cid,
                    Name = request.ContentName,
                    Description = request.Description,
                    CategoryId = request.CategoryId.Value
                },
                cancellationToken);
            content = await contentRepository.GetByIdAsync(createdContent.Id, cancellationToken)
                ?? throw new ContentOperationException(ContentFailure.NotFound);
        }

        var version = await versionRepository.GetByVidAsync(request.Vid, cancellationToken);
        if (version is not null && (version.ContentId != content.Id || version.Platform != request.Platform))
        {
            throw new ContentOperationException(
                ContentFailure.Conflict,
                $"VID {request.Vid:D} is already assigned to a different content item or platform.");
        }

        ContentVersionDto result;
        if (version is null)
        {
            result = await versionsService.Create(
                new CreateContentVersionDto
                {
                    Cid = request.Cid,
                    Vid = request.Vid,
                    ContentId = content.Id,
                    Name = request.VersionName,
                    Date = request.Date,
                    Status = "draft",
                    PublishStatus = PublishStatus.Draft,
                    Application = request.Application,
                    Platform = request.Platform,
                    ApplicationVersion = request.ApplicationVersion
                },
                cancellationToken);
        }
        else
        {
            result = Map(version);
        }

        var current = await versionRepository.GetByIdAsync(result.Id, cancellationToken)
            ?? throw new ContentOperationException(ContentFailure.NotFound);
        var uploads = new List<VersionFileUpload>();

        if (!current.Files.Any(file => file.Role == "primary"))
        {
            uploads.Add(request.PrimaryFile);
        }

        var knownFiles = current.Files
            .Select(file => $"{file.Role}\0{file.OriginalFileName}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var upload in requestedFiles.Skip(1))
        {
            var originalFileName = Path.GetFileName(upload.FileName);
            if (!knownFiles.Add($"{upload.Role}\0{originalFileName}"))
            {
                continue;
            }

            uploads.Add(upload);
        }

        if (uploads.Count > 0)
        {
            result = await versionsService.AddFiles(current.Id, uploads, cancellationToken);
        }

        return result;
    }

    private static VersionFileUpload ToVersionFileUpload(VersionImportAuxiliaryUpload file)
    {
        var role = string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase)
            ? "lookup-table"
            : "attachment";
        return new VersionFileUpload(file.FileName, file.Length, file.OpenReadStream, role, file.MetadataJson);
    }

    private static void Validate(VersionImportRequest request)
    {
        if (!Uuid7.IsValid(request.Cid) || !Uuid7.IsValid(request.Vid))
        {
            throw new ContentOperationException(ContentFailure.Validation, "CID and VID must be UUID v7 values.");
        }

        if (string.IsNullOrWhiteSpace(request.ContentName) ||
            string.IsNullOrWhiteSpace(request.VersionName) ||
            string.IsNullOrWhiteSpace(request.ApplicationVersion))
        {
            throw new ContentOperationException(
                ContentFailure.Validation,
                "ContentName, VersionName and ApplicationVersion are required.");
        }
    }

    private static ContentVersionDto Map(ContentVersion entity)
    {
        return new ContentVersionDto(
            entity.Id,
            entity.Vid,
            entity.ContentId,
            entity.Files.Select(file => file.Id).ToArray(),
            entity.Name,
            entity.Date,
            entity.Status,
            entity.PublishStatus,
            entity.Platform,
            entity.Application,
            entity.ApplicationVersion,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}

public sealed record VersionImportRequest(
    Guid Cid,
    Guid Vid,
    string ContentName,
    string? Description,
    int? CategoryId,
    string VersionName,
    DateTimeOffset Date,
    string ApplicationVersion,
    VersionFileUpload PrimaryFile,
    IReadOnlyCollection<VersionImportAuxiliaryUpload> AuxiliaryFiles,
    Platform Platform,
    string Application);

public sealed record VersionImportAuxiliaryUpload(
    string FileName,
    long Length,
    Func<Stream> OpenReadStream,
    string? MetadataJson);
