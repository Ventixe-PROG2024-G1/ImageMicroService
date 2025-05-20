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

namespace ImageServiceProvider.Tests
{
    public class AzureImageServiceTests : IDisposable // Implementera IDisposable för att rensa InMemory-databasen
    {
        private readonly Mock<BlobContainerClient> _mockBlobContainerClient;
        private readonly Mock<BlobClient> _mockBlobClient; // Vi behöver en mock för BlobClient också
        private readonly ImageDbContext _dbContext;
        private readonly Mock<ICacheHandler<ImageResponseModel>> _mockCacheHandler;
        private readonly AzureImageService _imageService;

        private const string TestConnectionString = "DefaultEndpointsProtocol=https;AccountName=fakestorage;AccountKey=fakekey;EndpointSuffix=core.windows.net";
        private const string TestContainerName = "test-images";

        public AzureImageServiceTests()
        {
            // --- Mocka Blob Storage ---
            _mockBlobClient = new Mock<BlobClient>();
            _mockBlobContainerClient = new Mock<BlobContainerClient>(new Uri($"https://fakestorage.blob.core.windows.net/{TestContainerName}"), null /* credentials can be null for mock */);

            // Setup GetBlobClient för att returnera vår mockade BlobClient
            _mockBlobContainerClient.Setup(c => c.GetBlobClient(It.IsAny<string>()))
                                    .Returns(_mockBlobClient.Object);

            // Setup för BlobClient-metoder (UploadAsync, DeleteIfExistsAsync, Uri)
            // Uri måste returnera en giltig Uri
            _mockBlobClient.Setup(b => b.Uri).Returns(new Uri($"https://fakestorage.blob.core.windows.net/{TestContainerName}/mockblob.jpg"));
            _mockBlobClient.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>()); // Returnera en mockad respons
            _mockBlobClient.Setup(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(Mock.Of<Response<bool>>(r => r.Value == true));


            // --- Setup InMemory DbContext ---
            var options = new DbContextOptionsBuilder<ImageDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unikt namn för varje testkörning
                .Options;
            _dbContext = new ImageDbContext(options);
            _dbContext.Database.EnsureCreated(); // Se till att schemat skapas

            // --- Mocka Cache Handler ---
            _mockCacheHandler = new Mock<ICacheHandler<ImageResponseModel>>();
            _imageService = new AzureImageService(_mockBlobContainerClient.Object, _dbContext, _mockCacheHandler.Object);
        }

        // Rensningsmetod för InMemory-databasen
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
            var imageId = Guid.Empty; // Kommer att sättas av tjänsten
            var originalName = "test.png";
            var contentType = "image/png";
            var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });

            ImageResponseModel? capturedCachedModel = null;
            _mockCacheHandler.Setup(c => c.SetCache(It.IsAny<string>(), It.IsAny<ImageResponseModel>(), It.IsAny<int>()))
                .Callback<string, ImageResponseModel, int>((key, model, mins) =>
                {
                    imageId = Guid.Parse(key); // Fånga ID från cache-nyckeln
                    capturedCachedModel = model;
                })
                .Returns((string key, ImageResponseModel model, int mins) => model);

            // Act
            var result = await _imageService.UploadFileAsync(fileStream, originalName, contentType);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.ImageId); // Kontrollera att ett nytt ID har genererats
            Assert.Equal(contentType, result.ContentType);
            Assert.Equal($"https://fakestorage.blob.core.windows.net/{TestContainerName}/mockblob.jpg", result.ImageUrl); // Kontrollera URL-format

            // Verifiera DB-interaktion
            var entityInDb = await _dbContext.Images.FindAsync(result.ImageId);
            Assert.NotNull(entityInDb);
            Assert.Equal(result.ImageId, entityInDb.ImageId);
            Assert.Equal($"{result.ImageId}.png", entityInDb.ImageBlobName);
            Assert.Equal(contentType, entityInDb.ContentType);

            // Verifiera Cache-interaktion
            _mockCacheHandler.Verify(c => c.SetCache(result.ImageId.ToString(), It.Is<ImageResponseModel>(m => m.ImageId == result.ImageId), It.IsAny<int>()), Times.Once);
            Assert.NotNull(capturedCachedModel);
            Assert.Equal(result.ImageId, capturedCachedModel.ImageId);

            _mockBlobClient.Verify(b => b.UploadAsync(
            It.IsAny<Stream>(),
            It.Is<BlobUploadOptions>(opts => opts.HttpHeaders.ContentType == contentType), // Verifiera att rätt ContentType sattes
            It.IsAny<CancellationToken>()),
            Times.Once);
        }

        [Fact]
        public async Task UploadFileAsync_WithSvgExtensionAndOctetStream_SetsContentTypeToSvgXml()
        {
            // Arrange
            var originalName = "drawing.svg";
            var providedContentType = "application/octet-stream"; // Eller null
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
            // Verifiera att DB inte anropades
            Assert.False(await _dbContext.Images.AnyAsync(i => i.ImageId == imageId)); // Förutsätter tom DB initialt för detta test
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
            _dbContext.ChangeTracker.Clear(); // Rensa tracking för att simulera ny hämtning
            var localMockBlobClientForThisTest = new Mock<BlobClient>(); // En specifik mock för detta anrop

            // Förväntad URL (denna del är svår att testa utan att kunna mocka blobClient.Uri korrekt)
            // Vi kan bara verifiera att en URL genereras.
            // string expectedUrlPattern = $"https://fakestorage.blob.core.windows.net/{TestContainerName}/{imageId}.jpg";
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

            // Verifiera att den cachades (GetOrCreateAsync anropades och factoryn kördes)
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

            // Vi kan inte enkelt verifiera blobClient.DeleteIfExistsAsync utan refaktorering.

            // Act
            var result = await _imageService.DeleteImageAsync(imageId);

            // Assert
            Assert.True(result);
            Assert.Null(await _dbContext.Images.FindAsync(imageId)); // Kontrollera att den är borta från DB
            _mockCacheHandler.Verify(c => c.RemoveCache(imageId.ToString()), Times.Once);

            _mockBlobContainerClient.Verify(c => c.GetBlobClient(blobNameToDelete), Times.Once);

            _mockBlobClient.Verify(b => b.DeleteIfExistsAsync(
                DeleteSnapshotsOption.None, // Default-värdet som AzureImageService använder
                null,                       // Default-värdet för BlobRequestConditions
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DeleteImageAsync_ImageDoesNotExist_ReturnsFalse()
        {
            // Arrange
            var imageId = Guid.NewGuid(); // Existerar inte i DB

            // Act
            var result = await _imageService.DeleteImageAsync(imageId);

            // Assert
            Assert.False(result);
            _mockCacheHandler.Verify(c => c.RemoveCache(It.IsAny<string>()), Times.Never); // Cache ska inte röras
        }
    }
}