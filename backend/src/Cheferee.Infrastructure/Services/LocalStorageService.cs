namespace Cheferee.Infrastructure.Services;

public sealed class LocalStorageService : IStorageService
{
    private readonly string _storagePath;
    private readonly string _baseUrl;

    public LocalStorageService()
    {
        _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "media");
        Directory.CreateDirectory(_storagePath);
        _baseUrl = "http://localhost:5000/media";
    }

    public string GeneratePreSignedUploadUrl(string objectKey, string contentType, TimeSpan expiry)
    {
        var fileName = Path.GetFileName(objectKey);
        return $"http://localhost:5000/api/v1/posts/upload-direct?file={fileName}";
    }

    public string GetPublicUrl(string objectKey)
    {
        var fileName = Path.GetFileName(objectKey);
        return $"{_baseUrl}/{fileName}";
    }
}