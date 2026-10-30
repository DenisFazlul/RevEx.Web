
namespace RevEx.Infrastructure.Storage;

public sealed class S3StorageOptions
{
    public string ServiceUrl { get; set; } = "https://storage.yandexcloud.net";
    public string Region { get; set; } = "ru-central1";
    public string BucketName { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = "revex/files";
}
