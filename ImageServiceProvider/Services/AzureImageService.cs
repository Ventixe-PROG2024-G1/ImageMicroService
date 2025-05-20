using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using ImageServiceProvider.Data.Entities;
using ImageServiceProvider.Models;
using ImageServiceProvider.Data.Context;
using ImageServiceProvider.Services.Handlers;

namespace ImageServiceProvider.Services;

public interface IAzureImageService
{
    Task<bool> DeleteImageAsync(Guid imageId);
    Task<ImageResponseModel?> GetImageByIdAsync(Guid imageId);
    Task<ImageResponseModel?> UploadFileAsync(Stream fileStream, string originalName, string providedContentType);
}

public class AzureImageService(BlobContainerClient blobContainerClient, ImageDbContext dbContext, ICacheHandler<ImageResponseModel> cacheHandler) : IAzureImageService
{
    private readonly BlobContainerClient _containerClient = blobContainerClient;
    private readonly ImageDbContext _dbContext = dbContext;
    private readonly ICacheHandler<ImageResponseModel> _cache = cacheHandler;

    public async Task<ImageResponseModel?> UploadFileAsync(Stream fileStream, string originalName, string providedContentType)
    {
        if (fileStream == null || fileStream.Length == 0)
            return null!;

        var fileExtension = Path.GetExtension(originalName);
        var imageId = Guid.NewGuid();
        var blobName = $"{imageId}{fileExtension}";

        if ((string.IsNullOrWhiteSpace(providedContentType) || providedContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            && fileExtension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            providedContentType = "image/svg+xml";
        }
        else if (string.IsNullOrWhiteSpace(providedContentType))
        {
            providedContentType = "application/octet-stream";
        }

        BlobClient blobClient = _containerClient.GetBlobClient(blobName);
        var uploadOption = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = providedContentType
            }
        };

        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        await blobClient.UploadAsync(fileStream, uploadOption);

        var imageEntity = new ImageEntity
        {
            ImageId = imageId,
            ImageBlobName = blobName,
            ContentType = providedContentType
        };

        _dbContext.Images.Add(imageEntity);
        await _dbContext.SaveChangesAsync();


        var model = new ImageResponseModel
        {
            ImageId = imageId,
            ImageUrl = blobClient.Uri.ToString(),
            ContentType = providedContentType
        };

        _cache.SetCache(imageId.ToString(), model);

        return model;
    }

    public async Task<bool> DeleteImageAsync(Guid imageId)
    {
        var imageEntity = await _dbContext.Images.FindAsync(imageId);
        if (imageEntity == null)
            return false;

        BlobClient blobClient = _containerClient.GetBlobClient(imageEntity.ImageBlobName);
        await blobClient.DeleteIfExistsAsync();
        _dbContext.Images.Remove(imageEntity);
        await _dbContext.SaveChangesAsync();
        _cache.RemoveCache(imageId.ToString());

        return true;
    }

    public async Task<ImageResponseModel?> GetImageByIdAsync(Guid imageId)
    {
        return await _cache.GetOrCreateAsync(imageId.ToString(), async () =>
        {
            var imageEntity = await _dbContext.Images.FindAsync(imageId);
            if (imageEntity == null)
                return null;

            BlobClient blobClient = _containerClient.GetBlobClient(imageEntity.ImageBlobName);
            var model = new ImageResponseModel
            {
                ImageId = imageId,
                ImageUrl = blobClient.Uri.ToString(),
                ContentType = imageEntity.ContentType
            };
            return model;
        });
    }
}