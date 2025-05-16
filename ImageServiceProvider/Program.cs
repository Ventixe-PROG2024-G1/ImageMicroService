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
builder.Services.AddDbContext<ImageDbContext>(x => x.UseSqlServer(Environment.GetEnvironmentVariable("ImageSqlConnection")));

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton(typeof(ICacheHandler<>), typeof(CacheHandler<>));
builder.Services.AddScoped<IAzureImageService, AzureImageService>();


builder.Build().Run();
