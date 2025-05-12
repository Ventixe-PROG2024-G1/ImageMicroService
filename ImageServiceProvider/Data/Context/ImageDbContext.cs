using ImageServiceProvider.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ImageServiceProvider.Data.Context;

public class ImageDbContext(DbContextOptions<ImageDbContext> options) : DbContext(options)
{
    public DbSet<ImageEntity> Images { get; set; }
}
