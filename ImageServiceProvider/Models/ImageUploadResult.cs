namespace ImageServiceProvider.Models;

public class ImageUploadResult
{
    public Guid ImageId { get; set; }
    public string BlobName { get; set; } = null!;
    public Uri BlobUri { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public string? OriginalFileName { get; set; }
}
