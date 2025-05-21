using ImageServiceProvider.Functions;
using ImageServiceProvider.Models;
using ImageServiceProvider.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using System.Text;

namespace ImageServiceProvider.Test.Functions;

public class ImageUploadFunctionTests
{
    private readonly Mock<IAzureImageService> _mockAzureImageService;
    private readonly ILogger<ImageUploadFunction> _logger; // Använd ILogger, NullLogger är en implementation
    private readonly ImageUploadFunction _function;

    public ImageUploadFunctionTests()
    {
        _mockAzureImageService = new Mock<IAzureImageService>();
        _logger = new Mock<ILogger<ImageUploadFunction>>().Object; // Eller NullLogger<ImageUploadFunction>.Instance;
        _function = new ImageUploadFunction(_logger, _mockAzureImageService.Object);
    }

    // Hjälpmetod för att skapa en mockad HttpRequest med en fil
    private DefaultHttpContext CreateHttpContextWithFile(string fileName, string contentType, byte[] fileContent, bool hasFormContentType = true)
    {
        var httpContext = new DefaultHttpContext();

        if (hasFormContentType)
        {
            var stream = new MemoryStream(fileContent);
            var formFile = new FormFile(stream, 0, stream.Length, "file", fileName) // "file" är namnet funktionen förväntar sig
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
            // formFile.Headers["Content-Disposition"] = $"form-data; name=\"file\"; filename=\"{fileName}\""; // Kan behövas i vissa scenarier

            var formFileCollection = new FormFileCollection { formFile };
            httpContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>(), formFileCollection);
            httpContext.Request.Headers["Content-Type"] = "multipart/form-data"; // Viktigt
        }
        else
        {
            // Om inte form content type, sätt en annan för att testa den grenen
            httpContext.Request.Headers["Content-Type"] = "application/json";
        }
        return httpContext;
    }

    // Hjälpmetod för att skapa en mockad HttpRequest utan fil (eller med tom fil)
    private DefaultHttpContext CreateHttpContextWithoutFile(bool hasFormContentType = true, bool fileIsEmpty = false)
    {
        var httpContext = new DefaultHttpContext();
        if (hasFormContentType)
        {
            var formFileCollection = new FormFileCollection();
            if (!fileIsEmpty) // Om vi testar "ingen fil alls"
            {
                // Ingen fil läggs till i formFileCollection
            }
            else // Om vi testar "tom fil"
            {
                var stream = new MemoryStream(); // Tom stream
                var formFile = new FormFile(stream, 0, stream.Length, "file", "empty.txt")
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "text/plain"
                };
                formFileCollection.Add(formFile);
            }
            httpContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>(), formFileCollection);
            httpContext.Request.Headers["Content-Type"] = "multipart/form-data";
        }
        else
        {
            httpContext.Request.Headers["Content-Type"] = "application/json";
        }
        return httpContext;
    }


    [Fact]
    public async Task UploadImage_WithValidFile_ReturnsCreatedAtActionResultAndCallsService()
    {
        // Arrange
        var fileName = "test.png";
        var contentType = "image/png";
        var fileContent = Encoding.UTF8.GetBytes("fake png data");
        var httpContext = CreateHttpContextWithFile(fileName, contentType, fileContent);
        var request = httpContext.Request;

        var expectedImageId = Guid.NewGuid();
        var expectedResponseModel = new ImageResponseModel
        {
            ImageId = expectedImageId,
            ImageUrl = $"http://example.com/images/{expectedImageId}.png",
            ContentType = contentType
        };

        _mockAzureImageService
            .Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), fileName, contentType))
            .ReturnsAsync(expectedResponseModel);

        // Act
        var result = await _function.UploadImage(request);

        // Assert
        var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, createdAtActionResult.StatusCode);
        Assert.Equal(nameof(ImageResponseFunction.GetImage), createdAtActionResult.ActionName);
        Assert.Equal("ImageResponseFunction", createdAtActionResult.ControllerName); // ControllerName är Function klassens namn

        var returnedValue = Assert.IsType<ImageResponseModel>(createdAtActionResult.Value);
        Assert.Equal(expectedImageId, returnedValue.ImageId);
        Assert.Equal(expectedResponseModel.ImageUrl, returnedValue.ImageUrl);

        Assert.NotNull(createdAtActionResult.RouteValues);
        Assert.True(createdAtActionResult.RouteValues.ContainsKey("imageId"));
        Assert.Equal(expectedImageId, createdAtActionResult.RouteValues["imageId"]);

        _mockAzureImageService.Verify(s => s.UploadFileAsync(
            It.Is<Stream>(st => st.Length == fileContent.Length), // Verifiera att streamen har rätt längd
            fileName,
            contentType), Times.Once);
    }

    [Fact]
    public async Task UploadImage_ServiceReturnsNull_ReturnsBadRequestObjectResult()
    {
        // Arrange
        var fileName = "test.jpg";
        var contentType = "image/jpeg";
        var fileContent = Encoding.UTF8.GetBytes("fake jpg data");
        var httpContext = CreateHttpContextWithFile(fileName, contentType, fileContent);
        var request = httpContext.Request;

        _mockAzureImageService
            .Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), fileName, contentType))
            .ReturnsAsync((ImageResponseModel?)null); // Servicen returnerar null

        // Act
        var result = await _function.UploadImage(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Failed to upload image or save image", badRequestResult.Value);
        _mockAzureImageService.Verify(s => s.UploadFileAsync(It.IsAny<Stream>(), fileName, contentType), Times.Once);
    }

    [Fact]
    public async Task UploadImage_NoFormContentType_ReturnsBadRequestObjectResult()
    {
        // Arrange
        var httpContext = CreateHttpContextWithFile("test.png", "image/png", new byte[0], hasFormContentType: false);
        var request = httpContext.Request;

        // Act
        var result = await _function.UploadImage(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Request does not contain form data", badRequestResult.Value);
        _mockAzureImageService.Verify(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadImage_NoFileInForm_ReturnsBadRequestObjectResult()
    {
        // Arrange
        var httpContext = CreateHttpContextWithoutFile(fileIsEmpty: false); // hasFormContentType är true by default, men ingen fil
        var request = httpContext.Request;

        // Act
        var result = await _function.UploadImage(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No file uploaded or file is empty", badRequestResult.Value);
        _mockAzureImageService.Verify(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadImage_EmptyFileInForm_ReturnsBadRequestObjectResult()
    {
        // Arrange
        var httpContext = CreateHttpContextWithoutFile(fileIsEmpty: true); // hasFormContentType är true by default, men filen är tom
        var request = httpContext.Request;


        // Act
        var result = await _function.UploadImage(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No file uploaded or file is empty", badRequestResult.Value); // Din funktion kollar formFile.Length == 0
        _mockAzureImageService.Verify(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
