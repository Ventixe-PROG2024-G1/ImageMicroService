using ImageServiceProvider.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ImageServiceProvider.Functions;

public class ImageDeleteFunction(ILogger<ImageDeleteFunction> logger, IAzureImageService azureImageService)
{
    private readonly ILogger<ImageDeleteFunction> _logger = logger;
    private readonly IAzureImageService _azureImageService = azureImageService;

    [Function("ImageDeleteFunction")]
    public async Task<IActionResult> DeleteImage([HttpTrigger(AuthorizationLevel.Function, "delete", Route = "images/{imageId:guid}")] HttpRequest req, Guid imageId)
    {
        _logger.LogInformation($"Starting image deletion process for image with ID: {imageId}");

        if (imageId == Guid.Empty)
        {
            _logger.LogWarning("Image ID is invalid");
            return new BadRequestObjectResult("Invalid image ID provided.");
        }

        var deleteResult = await _azureImageService.DeleteImageAsync(imageId);

        if (!deleteResult)
        {
            _logger.LogWarning("Deleting image failed");
            return new BadRequestObjectResult("Image did not successfully delete.");
        }

        _logger.LogInformation($"Image with ID: {imageId} deleted successfully");

        return new NoContentResult();
    }
}