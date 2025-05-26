using Azure.Storage.Blobs;
using ImageServiceProvider.Data.Context;
using ImageServiceProvider.Services;
using ImageServiceProvider.Services.Handlers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.Services.AddMemoryCache();
builder.Services.AddDbContext<ImageDbContext>(x => x.UseSqlServer(Environment.GetEnvironmentVariable("SqlConnection")));

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton(typeof(ICacheHandler<>), typeof(CacheHandler<>));
builder.Services.AddScoped<IAzureImageService, AzureImageService>();

string blobConnectionString = Environment.GetEnvironmentVariable("BlobStorage:ConnectionString") ?? throw new InvalidOperationException("BlobStorage:ConnectionString is not set in the environment variables.");
string blobContainerName = Environment.GetEnvironmentVariable("BlobStorage:ContainerName") ?? throw new InvalidOperationException("BlobStorage:ContainerName is not set in the environment variables.");

builder.Services.AddSingleton(new BlobContainerClient(blobConnectionString, blobContainerName));

builder.Build().Run();
