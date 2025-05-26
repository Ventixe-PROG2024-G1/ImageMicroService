using ImageServiceProvider.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ImageServiceProvider.Functions;

public class ImageResponseFunction(ILogger<ImageResponseFunction> logger, IAzureImageService azureImageService)
{
    private readonly ILogger<ImageResponseFunction> _logger = logger;
    private readonly IAzureImageService _azureImageService = azureImageService;

    [Function("ImageResponseFunction")]
    public async Task<IActionResult> GetImage([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "images/{imageId:guid}")] HttpRequest req, Guid imageId)
    {
        _logger.LogInformation($"Starting image response process for image with ID: {imageId}");

        var imageResponse = await _azureImageService.GetImageByIdAsync(imageId);
        if (imageResponse == null)
        {
            _logger.LogWarning($"Image with ID: {imageId} not found");
            return new NotFoundResult();
        }

        _logger.LogInformation($"Image with ID: {imageId} found, returning response");
        return new OkObjectResult(imageResponse);
    }
}