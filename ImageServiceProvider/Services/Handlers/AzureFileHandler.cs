using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using ImageServiceProvider.Models;

namespace ImageServiceProvider.Services.Handlers;

public class AzureFileHandler(string connectionString, string containerName)
{
    private readonly BlobContainerClient _containerClient = new(connectionString, containerName);


    public async Task<ImageUploadResult> UploadFileAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return null!;

        var fileExtension = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid()}{fileExtension}";

        string contentType = !string.IsNullOrEmpty(file.ContentType)
            ? file.ContentType
            : "application/octet-stream";

        if ((contentType == "application/octet-stream" || string.IsNullOrEmpty(contentType))
            && fileExtension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            contentType = "image/svg+xml";
        }

        BlobClient blobClient = _containerClient.GetBlobClient(fileName);
        var uploadOption = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            }
        };

        using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, uploadOption);

        return blobClient.Uri.ToString();
    }
}
