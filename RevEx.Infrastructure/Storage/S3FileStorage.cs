using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using RevEx.Application.Abstractions.Storage;

namespace RevEx.Infrastructure.Storage;

public sealed class S3FileStorage : IFileStorage, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly string _bucketName;
    private readonly string _keyPrefix;

    public S3FileStorage(S3StorageOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ServiceUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Region);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.BucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.AccessKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SecretKey);

        _bucketName = options.BucketName;
        _keyPrefix = options.KeyPrefix.Trim('/');
        _client = new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKey, options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = options.ServiceUrl,
                AuthenticationRegion = options.Region,
                ForcePathStyle = false
            });
    }

    public async Task SaveAsync(int fileId, Stream content, CancellationToken cancellationToken)
    {
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = GetKey(fileId),
            InputStream = content,
            AutoCloseStream = false
        }, cancellationToken);
    }

    public async Task<Stream?> OpenReadAsync(int fileId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucketName, GetKey(fileId), cancellationToken);
            return new S3ResponseStream(response);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(int fileId, CancellationToken cancellationToken)
    {
        await _client.DeleteObjectAsync(_bucketName, GetKey(fileId), cancellationToken);
    }

    public void Dispose() => _client.Dispose();

    private string GetKey(int fileId)
    {
        if (fileId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fileId), "File id must be positive.");
        }

        return string.IsNullOrEmpty(_keyPrefix) ? $"{fileId}.bin" : $"{_keyPrefix}/{fileId}.bin";
    }

    private sealed class S3ResponseStream(GetObjectResponse response) : Stream
    {
        private readonly Stream _inner = response.ResponseStream;

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                response.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            response.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
