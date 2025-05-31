using Azure.Storage.Blobs;
using ImageServiceProvider.Data.Context;
using ImageServiceProvider.Models;
using ImageServiceProvider.Services;
using ImageServiceProvider.Services.Handlers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.Azurite;

namespace ImageServiceProvider.Test.Services;


// Majoriteten är Genererad av AI genom Copilot unit test genom unit test feature.
public class AzureImageServiceIntegrationTest : IAsyncLifetime
{
    private readonly AzuriteContainer _azuriteContainer = new AzuriteBuilder()
        .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
        .Build();
    private BlobContainerClient _blobContainerClient = null!;
    private ImageDbContext _dbContext = null!;
    private AzureImageService _azureImageService = null!;
    private Mock<ICacheHandler<ImageResponseModel>> _mockCacheHandler = null!;

    public async Task InitializeAsync()
    {
        await _azuriteContainer.StartAsync();

        var blobServiceClient = new BlobServiceClient(_azuriteContainer.GetConnectionString());
        _blobContainerClient = blobServiceClient.GetBlobContainerClient("images");
        await _blobContainerClient.CreateIfNotExistsAsync();

        var dbContextOptions = new DbContextOptionsBuilder<ImageDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ImageDbContext(dbContextOptions);

        _mockCacheHandler = new Mock<ICacheHandler<ImageResponseModel>>();

        _mockCacheHandler.Setup(c => c.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<Func<Task<ImageResponseModel?>>>(), It.IsAny<int>()))
            .Returns((string cacheKey, Func<Task<ImageResponseModel?>> factory, int minutesToCache) => factory());


        _azureImageService = new AzureImageService(_blobContainerClient, _dbContext, _mockCacheHandler.Object);
    }

    public async Task DisposeAsync()
    {
        await _azuriteContainer.DisposeAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task UploadFileAsync_ShouldUploadImageAndSaveMetadata()
    {
        // Arrange
        var originalFileName = "testimage.png";
        var contentType = "image/png";
        await using var memoryStream = new MemoryStream("dummy image data"u8.ToArray());

        // Act
        var result = await _azureImageService.UploadFileAsync(memoryStream, originalFileName, contentType);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(contentType, result.ContentType);
        Assert.Contains(result.ImageId.ToString(), result.ImageUrl);
        Assert.EndsWith(Path.GetExtension(originalFileName), result.ImageUrl);

        var entity = await _dbContext.Images.FindAsync(result.ImageId);
        Assert.NotNull(entity);
        Assert.Equal(result.ImageId, entity.ImageId);
        Assert.Equal(contentType, entity.ContentType);
        Assert.Equal($"{result.ImageId}{Path.GetExtension(originalFileName)}", entity.ImageBlobName);

        var blobClient = _blobContainerClient.GetBlobClient(entity.ImageBlobName);
        Assert.True(await blobClient.ExistsAsync());
        var properties = await blobClient.GetPropertiesAsync();
        Assert.Equal(contentType, properties.Value.ContentType);

        _mockCacheHandler.Verify(c => c.SetCache(result.ImageId.ToString(), It.Is<ImageResponseModel>(m => m.ImageId == result.ImageId), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task GetImageByIdAsync_ShouldReturnImage_WhenImageExists()
    {
        // Arrange
        var originalFileName = "testimage.jpg";
        var contentType = "image/jpeg";
        await using var memoryStream = new MemoryStream("dummy jpeg data"u8.ToArray());

        var uploadedImage = await _azureImageService.UploadFileAsync(memoryStream, originalFileName, contentType);
        Assert.NotNull(uploadedImage);

        // Act
        var result = await _azureImageService.GetImageByIdAsync(uploadedImage.ImageId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(uploadedImage.ImageId, result.ImageId);
        Assert.Equal(uploadedImage.ImageUrl, result.ImageUrl);
        Assert.Equal(uploadedImage.ContentType, result.ContentType);

        _mockCacheHandler.Verify(c => c.GetOrCreateAsync(uploadedImage.ImageId.ToString(), It.IsAny<Func<Task<ImageResponseModel?>>>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task GetImageByIdAsync_ShouldReturnNull_WhenImageDoesNotExist()
    {
        // Arrange
        var nonExistentImageId = Guid.NewGuid();

        // Act
        var result = await _azureImageService.GetImageByIdAsync(nonExistentImageId);

        // Assert
        Assert.Null(result);
        _mockCacheHandler.Verify(c => c.GetOrCreateAsync(nonExistentImageId.ToString(), It.IsAny<Func<Task<ImageResponseModel?>>>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task DeleteImageAsync_ShouldDeleteImageAndMetadata_WhenImageExists()
    {
        // Arrange
        var originalFileName = "testimage.gif";
        var contentType = "image/gif";
        await using var memoryStream = new MemoryStream("dummy gif data"u8.ToArray());

        var uploadedImage = await _azureImageService.UploadFileAsync(memoryStream, originalFileName, contentType);
        Assert.NotNull(uploadedImage);

        var entityBeforeDelete = await _dbContext.Images.FindAsync(uploadedImage.ImageId);
        Assert.NotNull(entityBeforeDelete);
        var blobName = entityBeforeDelete.ImageBlobName;

        // Act
        var deleteResult = await _azureImageService.DeleteImageAsync(uploadedImage.ImageId);

        // Assert
        Assert.True(deleteResult);

        var blobClient = _blobContainerClient.GetBlobClient(blobName);
        Assert.False(await blobClient.ExistsAsync());

        var entityAfterDelete = await _dbContext.Images.FindAsync(uploadedImage.ImageId);
        Assert.Null(entityAfterDelete);

        _mockCacheHandler.Verify(c => c.RemoveCache(uploadedImage.ImageId.ToString()), Times.Once);
    }

    [Fact]
    public async Task DeleteImageAsync_ShouldReturnFalse_WhenImageDoesNotExist()
    {
        // Arrange
        var nonExistentImageId = Guid.NewGuid();

        // Act
        var result = await _azureImageService.DeleteImageAsync(nonExistentImageId);

        // Assert
        Assert.False(result);
        _mockCacheHandler.Verify(c => c.RemoveCache(nonExistentImageId.ToString()), Times.Never);
    }

    [Fact]
    public async Task UploadFileAsync_ShouldHandleSvgContentType()
    {
        // Arrange
        var originalFileNameSvg = "testimage.svg";
        var providedContentTypeOctet = "application/octet-stream";
        var providedContentTypeEmpty = "";
        var expectedContentTypeSvg = "image/svg+xml";

        await using var memoryStreamOctet = new MemoryStream("<svg></svg>"u8.ToArray());
        await using var memoryStreamEmpty = new MemoryStream("<svg></svg>"u8.ToArray());

        // Act & Assert for octet-stream
        var resultOctet = await _azureImageService.UploadFileAsync(memoryStreamOctet, originalFileNameSvg, providedContentTypeOctet);
        Assert.NotNull(resultOctet);
        Assert.Equal(expectedContentTypeSvg, resultOctet.ContentType);
        var entityOctet = await _dbContext.Images.FindAsync(resultOctet.ImageId);
        Assert.NotNull(entityOctet);
        var blobClientOctet = _blobContainerClient.GetBlobClient(entityOctet.ImageBlobName);
        var propertiesOctet = await blobClientOctet.GetPropertiesAsync();
        Assert.Equal(expectedContentTypeSvg, propertiesOctet.Value.ContentType);
        _mockCacheHandler.Verify(c => c.SetCache(resultOctet.ImageId.ToString(), It.IsAny<ImageResponseModel>(), It.IsAny<int>()), Times.Once);


        // Act & Assert for empty content type
        var resultEmpty = await _azureImageService.UploadFileAsync(memoryStreamEmpty, originalFileNameSvg, providedContentTypeEmpty);
        Assert.NotNull(resultEmpty);
        Assert.Equal(expectedContentTypeSvg, resultEmpty.ContentType);
        var entityEmpty = await _dbContext.Images.FindAsync(resultEmpty.ImageId);
        Assert.NotNull(entityEmpty);
        var blobClientEmpty = _blobContainerClient.GetBlobClient(entityEmpty.ImageBlobName);
        var propertiesEmpty = await blobClientEmpty.GetPropertiesAsync();
        Assert.Equal(expectedContentTypeSvg, propertiesEmpty.Value.ContentType);
        _mockCacheHandler.Verify(c => c.SetCache(resultEmpty.ImageId.ToString(), It.IsAny<ImageResponseModel>(), It.IsAny<int>()), Times.Once);
    }
}