using ImageServiceProvider.Models;
using ImageServiceProvider.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;


namespace ImageServiceProvider.Functions;

public class ImageUploadFunction(ILogger<ImageUploadFunction> logger, IAzureImageService azureImageService)
{
    private readonly ILogger<ImageUploadFunction> _logger = logger;
    private readonly IAzureImageService _azureImageService = azureImageService;

    [Function("ImageUploadFunction")]
    public async Task<IActionResult> UploadImage([HttpTrigger(AuthorizationLevel.Function, "post", Route = "images")] HttpRequest req)
    {
        _logger.LogInformation("Starting image upload process");

        if (!req.HasFormContentType)
        {
            _logger.LogWarning("Request does not contain form data");
            return new BadRequestObjectResult("Request does not contain form data");
        }

        IFormFile? formFile = req.Form.Files.FirstOrDefault();

        if (formFile == null || formFile.Length == 0)
        {
            _logger.LogWarning("No file found in the request");
            return new BadRequestObjectResult("No file uploaded or file is empty");
        }

        string originalFileName = formFile.FileName;
        string contentType = formFile.ContentType;
        await using Stream fileStream = formFile.OpenReadStream();
        _logger.LogInformation($"Attempting to upload file: {originalFileName}, Content Type: {contentType}");

        ImageResponseModel? imageResponse = await _azureImageService.UploadFileAsync(fileStream, originalFileName, contentType);

        if (imageResponse == null)
        {
            _logger.LogError("Failed to upload or save image");
            return new BadRequestObjectResult("Failed to upload image or save image");
        }

        _logger.LogInformation($"Image uploaded successfully: {imageResponse.ImageId}");

        return new CreatedAtActionResult(
            nameof(ImageResponseFunction.GetImage),
            "ImageResponseFunction",
            new { imageId = imageResponse.ImageId },
            imageResponse);
    }
}