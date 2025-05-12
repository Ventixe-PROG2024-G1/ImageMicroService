using System.ComponentModel.DataAnnotations;

namespace ImageServiceProvider.Data.Entities;

public class ImageEntity
{
    [Key]
    public Guid ImageId { get; set; } = Guid.NewGuid();

    [Required]
    public string ImageBlobName { get; set; } = null!;

    [Required]
    public string ContentType { get; set; } = null!;

    [Required]
    public string OriginalFileName { get; set; } = null!;
}
