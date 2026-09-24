namespace RevEx.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string Provider { get; set; } = "Memory";
    public NetworkFolderStorageOptions NetworkFolder { get; set; } = new();
    public S3StorageOptions S3 { get; set; } = new();
}

public sealed class NetworkFolderStorageOptions
{
    public string Path { get; set; } = string.Empty;
}

public sealed class S3StorageOptions
{
    public string ServiceUrl { get; set; } = "https://storage.yandexcloud.net";
    public string Region { get; set; } = "ru-central1";
    public string BucketName { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = "revex/files";
}
