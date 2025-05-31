using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ImageServiceProvider.Data.Context;
using ImageServiceProvider.Data.Entities;
using ImageServiceProvider.Models;
using ImageServiceProvider.Services;
using ImageServiceProvider.Services.Handlers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ImageServiceProvider.Test.Services;

// Majoriteten är Genererad av AI genom Copilot unit test genom unit test feature.
public class AzureImageServiceTests : IDisposable
{
    private readonly Mock<BlobContainerClient> _mockBlobContainerClient;
    private readonly Mock<BlobClient> _mockBlobClient;
    private readonly ImageDbContext _dbContext;
    private readonly Mock<ICacheHandler<ImageResponseModel>> _mockCacheHandler;
    private readonly AzureImageService _imageService;

    private const string TestConnectionString = "DefaultEndpointsProtocol=https;AccountName=fakestorage;AccountKey=fakekey;EndpointSuffix=core.windows.net";
    private const string TestContainerName = "test-images";

    public AzureImageServiceTests()
    {
        _mockBlobClient = new Mock<BlobClient>();
        _mockBlobContainerClient = new Mock<BlobContainerClient>(new Uri($"https://fakestorage.blob.core.windows.net/{TestContainerName}"), null);

        _mockBlobContainerClient.Setup(c => c.GetBlobClient(It.IsAny<string>()))
                                .Returns(_mockBlobClient.Object);

        _mockBlobClient.Setup(b => b.Uri).Returns(new Uri($"https://fakestorage.blob.core.windows.net/{TestContainerName}/mockblob.jpg"));
        _mockBlobClient.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());
        _mockBlobClient.Setup(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(Mock.Of<Response<bool>>(r => r.Value == true));


        var options = new DbContextOptionsBuilder<ImageDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ImageDbContext(options);
        _dbContext.Database.EnsureCreated();

        _mockCacheHandler = new Mock<ICacheHandler<ImageResponseModel>>();
        _imageService = new AzureImageService(_mockBlobContainerClient.Object, _dbContext, _mockCacheHandler.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    // --- Tester för UploadFileAsync ---
    [Fact]
    public async Task UploadFileAsync_WithValidStream_SavesToDbAndCacheAndReturnsModel()
    {
        // Arrange
        var originalName = "test.png";
        var fileExtension = ".png";
        var contentType = "image/png";
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });

        ImageResponseModel? capturedCachedModel = null;

        _mockCacheHandler.Setup(c => c.SetCache(It.IsAny<string>(), It.IsAny<ImageResponseModel>(), It.IsAny<int>()))
            .Callback<string, ImageResponseModel, int>((key, model, mins) =>
            {
                capturedCachedModel = model;
            })
            .Returns((string key, ImageResponseModel model, int mins) => model);

        string actualBlobNameUsedByService = string.Empty;

        _mockBlobContainerClient
            .Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Callback<string>(generatedBlobName => actualBlobNameUsedByService = generatedBlobName)
            .Returns(_mockBlobClient.Object);

        _mockBlobClient
            .Setup(b => b.Uri)
            .Returns(() => new Uri($"https://fakestorage.blob.core.windows.net/{TestContainerName}/{actualBlobNameUsedByService}"));

        // Act
        var result = await _imageService.UploadFileAsync(fileStream, originalName, contentType);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.ImageId);
        Assert.Equal(contentType, result.ContentType);

        var expectedImageUrl = $"https://fakestorage.blob.core.windows.net/{TestContainerName}/{result.ImageId}{fileExtension}";
        Assert.Equal(expectedImageUrl, result.ImageUrl);

        var entityInDb = await _dbContext.Images.FindAsync(result.ImageId);
        Assert.NotNull(entityInDb);
        Assert.Equal(result.ImageId, entityInDb.ImageId);
        Assert.Equal($"{result.ImageId}{fileExtension}", entityInDb.ImageBlobName);
        Assert.Equal(contentType, entityInDb.ContentType);

        _mockCacheHandler.Verify(c => c.SetCache(result.ImageId.ToString(), It.Is<ImageResponseModel>(m => m.ImageId == result.ImageId && m.ImageUrl == expectedImageUrl), It.IsAny<int>()), Times.Once);
        Assert.NotNull(capturedCachedModel);
        Assert.Equal(result.ImageId, capturedCachedModel.ImageId);
        Assert.Equal(expectedImageUrl, capturedCachedModel.ImageUrl);

        _mockBlobClient.Verify(b => b.UploadAsync(
            It.IsAny<Stream>(),
            It.Is<BlobUploadOptions>(opts => opts.HttpHeaders.ContentType == contentType),
            It.IsAny<CancellationToken>()),
            Times.Once);


        _mockBlobContainerClient.Verify(c => c.GetBlobClient(actualBlobNameUsedByService), Times.Once);
        Assert.Equal($"{result.ImageId}{fileExtension}", actualBlobNameUsedByService);
    }

    [Fact]
    public async Task UploadFileAsync_WithSvgExtensionAndOctetStream_SetsContentTypeToSvgXml()
    {
        // Arrange
        var originalName = "drawing.svg";
        var providedContentType = "application/octet-stream";
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var expectedContentType = "image/svg+xml";

        // Act
        var result = await _imageService.UploadFileAsync(fileStream, originalName, providedContentType);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedContentType, result.ContentType);

        var entityInDb = await _dbContext.Images.FindAsync(result.ImageId);
        Assert.NotNull(entityInDb);
        Assert.Equal(expectedContentType, entityInDb.ContentType);

        _mockCacheHandler.Verify(c => c.SetCache(result.ImageId.ToString(), It.Is<ImageResponseModel>(m => m.ContentType == expectedContentType), It.IsAny<int>()), Times.Once);
        _mockBlobClient.Verify(b => b.UploadAsync(
        It.IsAny<Stream>(),
        It.Is<BlobUploadOptions>(opts => opts.HttpHeaders.ContentType == expectedContentType),
        It.IsAny<CancellationToken>()),
        Times.Once);

        var expectedBlobName = $"{result.ImageId}{Path.GetExtension(originalName)}";
        _mockBlobContainerClient.Verify(c => c.GetBlobClient(expectedBlobName), Times.Once);
    }


    [Fact]
    public async Task UploadFileAsync_NullOrEmptyStream_ReturnsNull()
    {
        // Arrange
        Stream? nullStream = null;
        var emptyStream = new MemoryStream();

        // Act
        var resultNull = await _imageService.UploadFileAsync(nullStream!, "test.png", "image/png");
        var resultEmpty = await _imageService.UploadFileAsync(emptyStream, "test.png", "image/png");

        // Assert
        Assert.Null(resultNull);
        Assert.Null(resultEmpty);
    }


    // --- Tester för GetImageByIdAsync ---
    [Fact]
    public async Task GetImageByIdAsync_WhenInCache_ReturnsModelFromCache()
    {
        // Arrange
        var imageId = Guid.NewGuid();
        var cachedModel = new ImageResponseModel { ImageId = imageId, ImageUrl = "http://cached.url/img.jpg", ContentType = "image/jpeg" };

        _mockCacheHandler.Setup(c => c.GetOrCreateAsync(imageId.ToString(), It.IsAny<Func<Task<ImageResponseModel?>>>(), It.IsAny<int>()))
            .ReturnsAsync(cachedModel);

        // Act
        var result = await _imageService.GetImageByIdAsync(imageId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(cachedModel.ImageUrl, result.ImageUrl);
        Assert.False(await _dbContext.Images.AnyAsync(i => i.ImageId == imageId));
    }

    [Fact]
    public async Task GetImageByIdAsync_WhenNotInCacheButInDb_ReturnsModelFromDbAndCachesIt()
    {
        // Arrange
        var imageId = Guid.NewGuid();
        var blobNameInDb = $"{imageId}.jpg";
        var entityInDb = new ImageEntity { ImageId = imageId, ImageBlobName = $"{imageId}.jpg", ContentType = "image/jpeg" };
        await _dbContext.Images.AddAsync(entityInDb);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
        var localMockBlobClientForThisTest = new Mock<BlobClient>();

        var expectedBlobUri = new Uri($"https://fakestorage.blob.core.windows.net/{TestContainerName}/{blobNameInDb}");
        localMockBlobClientForThisTest.Setup(b => b.Uri).Returns(expectedBlobUri);


        _mockCacheHandler.Setup(c => c.GetOrCreateAsync(imageId.ToString(), It.IsAny<Func<Task<ImageResponseModel?>>>(), It.IsAny<int>()))
            .Returns(async (string key, Func<Task<ImageResponseModel?>> factory, int mins) => await factory()); // Kör factoryn
        _mockBlobContainerClient.Setup(c => c.GetBlobClient(blobNameInDb))
                    .Returns(localMockBlobClientForThisTest.Object);

        // Act
        var result = await _imageService.GetImageByIdAsync(imageId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(imageId, result.ImageId);
        Assert.Equal(entityInDb.ContentType, result.ContentType);
        Assert.Equal(expectedBlobUri.ToString(), result.ImageUrl);

        _mockCacheHandler.Verify(c => c.GetOrCreateAsync(imageId.ToString(), It.IsAny<Func<Task<ImageResponseModel?>>>(), It.IsAny<int>()), Times.Once);
        _mockBlobContainerClient.Verify(c => c.GetBlobClient(blobNameInDb), Times.Once);
    }

    [Fact]
    public async Task GetImageByIdAsync_WhenNotInCacheAndNotInDb_ReturnsNull()
    {
        // Arrange
        var imageId = Guid.NewGuid();
        _mockCacheHandler.Setup(c => c.GetOrCreateAsync(imageId.ToString(), It.IsAny<Func<Task<ImageResponseModel?>>>(), It.IsAny<int>()))
            .Returns(async (string key, Func<Task<ImageResponseModel?>> factory, int mins) => await factory());

        // Act
        var result = await _imageService.GetImageByIdAsync(imageId);

        // Assert
        Assert.Null(result);
    }

    // --- Tester för DeleteImageAsync ---
    [Fact]
    public async Task DeleteImageAsync_ImageExists_DeletesFromDbAndCacheAndReturnsTrue()
    {
        // Arrange
        var imageId = Guid.NewGuid();
        var blobNameToDelete = $"{imageId}.png";
        var entityInDb = new ImageEntity { ImageId = imageId, ImageBlobName = $"{imageId}.png", ContentType = "image/png" };
        await _dbContext.Images.AddAsync(entityInDb);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _imageService.DeleteImageAsync(imageId);

        // Assert
        Assert.True(result);
        Assert.Null(await _dbContext.Images.FindAsync(imageId));
        _mockCacheHandler.Verify(c => c.RemoveCache(imageId.ToString()), Times.Once);

        _mockBlobContainerClient.Verify(c => c.GetBlobClient(blobNameToDelete), Times.Once);

        _mockBlobClient.Verify(b => b.DeleteIfExistsAsync(
            DeleteSnapshotsOption.None,
            null,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteImageAsync_ImageDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var imageId = Guid.NewGuid();

        // Act
        var result = await _imageService.DeleteImageAsync(imageId);

        // Assert
        Assert.False(result);
        _mockCacheHandler.Verify(c => c.RemoveCache(It.IsAny<string>()), Times.Never);
    }
}