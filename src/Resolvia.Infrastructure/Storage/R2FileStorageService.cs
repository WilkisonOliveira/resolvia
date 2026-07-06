using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Resolvia.Application.Interfaces;

namespace Resolvia.Infrastructure.Storage;

public class R2FileStorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _publicEndpoint;
    private readonly string _publicUrl;

    public R2FileStorageService(IConfiguration configuration)
    {
        var accessKey = configuration["R2Storage:AccessKey"]!;
        var secretKey = configuration["R2Storage:SecretKey"]!;
        var endpoint = configuration["R2Storage:Endpoint"]!;
        _bucketName = configuration["R2Storage:BucketName"]!;
        _publicUrl = configuration["R2Storage:PublicUrl"]!;
        _publicEndpoint = endpoint;

        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true, // necessário para compatibilidade com R2
            RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED
        };

        _s3Client = new AmazonS3Client(accessKey, secretKey, config);
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
    {
        var key = $"{Guid.NewGuid()}_{fileName}"; // evita colisão de nomes

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = fileStream,
            ContentType = contentType,
            UseChunkEncoding = false // R2 não implementa STREAMING-AWS4-HMAC-SHA256-PAYLOAD
        };

        await _s3Client.PutObjectAsync(request);

        return $"{_publicUrl}/{key}";
    }

    public async Task DeleteFileAsync(string fileUrl)
    {
        var key = fileUrl.Split($"{_publicUrl}/").Last();

        await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        });
    }
}