using System.ComponentModel.DataAnnotations;

namespace ImageServiceProvider.Data.Entities;

public class ImageEntity
{
    [Key]
    public Guid ImageId { get; set; }

    [Required]
    public string ImageBlobName { get; set; } = null!;

    [Required]
    public string ContentType { get; set; } = null!;
}
