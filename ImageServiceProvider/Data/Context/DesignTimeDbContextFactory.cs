using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ImageServiceProvider.Data.Context;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ImageDbContext>
{
    public ImageDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("local.settings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration["Values:ImageSqlConnection"];
        var optionsBuilder = new DbContextOptionsBuilder<ImageDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        return new ImageDbContext(optionsBuilder.Options);
    }
}