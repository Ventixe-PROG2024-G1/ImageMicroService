//using Azure.Storage.Blobs.Models;
//using Azure.Storage.Blobs;
//using ImageServiceProvider.Data.Entities;
//using ImageServiceProvider.Models;
//using ImageServiceProvider.Data.Context;
//using ImageServiceProvider.Services.Handlers;

//namespace ImageServiceProvider.Services;

//public class AzureImageService(string connectionString, string containerName, ImageDbContext dbContext, ICacheHandler<AzureImageService> cacheHandler)
//{
//    private readonly BlobContainerClient _containerClient = new(connectionString, containerName);
//    private readonly ImageDbContext _dbContext = dbContext;
//    private readonly ICacheHandler<AzureImageService> _cacheHandler = cacheHandler;
//    private const string _cacheKey = "AzureImageServiceCacheKey";


//    public async Task<ImageResponseModel?> UploadFileAsync(Stream fileStream, string originalName, string providedContentType)
//    {
//        if (fileStream == null || fileStream.Length == 0)
//            return null!;

//        var fileExtension = Path.GetExtension(originalName);
//        var imageId = Guid.NewGuid();
//        var blobName = $"{imageId}{fileExtension}";

//        if ((string.IsNullOrWhiteSpace(providedContentType) || providedContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
//            && fileExtension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
//        {
//            providedContentType = "image/svg+xml";
//        }
//        else if (string.IsNullOrWhiteSpace(providedContentType))
//        {
//            providedContentType = "application/octet-stream";
//        }

//        BlobClient blobClient = _containerClient.GetBlobClient(blobName);
//        var uploadOption = new BlobUploadOptions
//        {
//            HttpHeaders = new BlobHttpHeaders
//            {
//                ContentType = providedContentType
//            }
//        };

//        if (fileStream.CanSeek)
//        {
//            fileStream.Position = 0;
//        }

//        await blobClient.UploadAsync(fileStream, uploadOption);

//        var imageEntity = new ImageEntity
//        {
//            ImageId = imageId,
//            ImageBlobName = blobName,
//            ContentType = providedContentType
//        };

//        _dbContext.Images.Add(imageEntity);
//        await _dbContext.SaveChangesAsync();

//        return new ImageResponseModel
//        {
//            ImageId = imageId,
//            ImageUrl = blobClient.Uri.ToString()
//        };
//    }

//    public async Task<bool> DeleteImageAsync(Guid imageId)
//    {
//        var imageEntity = await _dbContext.Images.FindAsync(imageId);
//        if (imageEntity == null)
//            return false;

//        BlobClient blobClient = _containerClient.GetBlobClient(imageEntity.ImageBlobName);
//        await blobClient.DeleteIfExistsAsync();
//        _dbContext.Images.Remove(imageEntity);
//        await _dbContext.SaveChangesAsync();

//        return true;
//    }

//    public async Task<ImageResponseModel?> GetImageByIdAsync(Guid imageId)
//    {
//        var imageEntity = await _dbContext.Images.FindAsync(imageId);
//        if (imageEntity == null)
//            return null;

//        BlobClient blobClient = _containerClient.GetBlobClient(imageEntity.ImageBlobName);

//        return new ImageResponseModel
//        {
//            ImageId = imageEntity.ImageId,
//            ImageUrl = blobClient.Uri.ToString(),
//            ContentType = imageEntity.ContentType
//        };
//    }
//}