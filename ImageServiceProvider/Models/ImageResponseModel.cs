namespace ImageServiceProvider.Models;

public class ImageResponseModel
{
    public Guid ImageId { get; set; }
    public string? ImageUrl { get; set; }
    public string? ContentType { get; set; }
    public string? ErrorMessage { get; set; }
}
