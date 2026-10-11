using Amazon.S3;
using Amazon.S3.Model;
using Cheferee.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Cheferee.Infrastructure.Services;

public interface IStorageService
{
    string GeneratePreSignedUploadUrl(string objectKey, string contentType, TimeSpan expiry);
    string GetPublicUrl(string objectKey);
}

public sealed class S3StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly StorageOptions _storageOptions;

    public S3StorageService(IOptions<StorageOptions> storageOptions)
    {
        _storageOptions = storageOptions.Value;

        var config = new AmazonS3Config
        {
            ServiceURL = _storageOptions.ServiceUrl,
            ForcePathStyle = true
        };

        _s3Client = new AmazonS3Client(_storageOptions.AccessKey, _storageOptions.SecretKey, config);
    }

    public string GeneratePreSignedUploadUrl(string objectKey, string contentType, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _storageOptions.BucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = DateTime.UtcNow.Add(expiry)
        };

        return _s3Client.GetPreSignedURL(request);
    }

    public string GetPublicUrl(string objectKey)
    {
        return $"{_storageOptions.ServiceUrl}/{_storageOptions.BucketName}/{objectKey}";
    }
}