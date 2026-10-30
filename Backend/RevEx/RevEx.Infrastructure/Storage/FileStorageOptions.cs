
namespace RevEx.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string Provider { get; set; } = "Memory";
    public NetworkFolderStorageOptions NetworkFolder { get; set; } = new();
    public S3StorageOptions S3 { get; set; } = new();
}
