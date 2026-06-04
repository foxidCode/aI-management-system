using Minio;
using Minio.DataModel.Args;

namespace backend.Services;

public class MinioService
{
    private readonly IMinioClient _client;
    private readonly string _bucketName;

    public MinioService(IConfiguration config)
    {
        var endpoint = config["Minio:Endpoint"] ?? "localhost:9000";
        var accessKey = config["Minio:AccessKey"] ?? "minioadmin";
        var secretKey = config["Minio:SecretKey"] ?? "minioadmin";
        var useSsl = bool.Parse(config["Minio:UseSSL"] ?? "false");
        _bucketName = config["Minio:BucketName"] ?? "inbound-attachments";

        _client = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSsl)
            .Build();

        EnsureBucketAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureBucketAsync()
    {
        var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucketName));
        if (!exists)
        {
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucketName));
        }
    }

    public async Task<string> UploadAsync(Stream stream, string objectKey, string contentType, long size)
    {
        var args = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectKey)
            .WithStreamData(stream)
            .WithObjectSize(size)
            .WithContentType(contentType);
        await _client.PutObjectAsync(args);
        return objectKey;
    }

    public async Task<Stream> DownloadAsync(string objectKey)
    {
        var ms = new MemoryStream();
        var args = new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectKey)
            .WithCallbackStream(s => s.CopyTo(ms));
        await _client.GetObjectAsync(args);
        ms.Position = 0;
        return ms;
    }

    public async Task DeleteAsync(string objectKey)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectKey);
        await _client.RemoveObjectAsync(args);
    }

    public string GetBucketName() => _bucketName;
}
