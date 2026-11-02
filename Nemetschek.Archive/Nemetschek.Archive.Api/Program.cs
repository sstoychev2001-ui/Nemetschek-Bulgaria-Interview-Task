using System.Globalization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpLogging;
using Nemetschek.Archive.Api.Contracts.Shared;
using Nemetschek.Archive.Api.Middleware;
using Nemetschek.Archive.Core.Services.Uploads;
using Nemetschek.Archive.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod
                            | HttpLoggingFields.RequestPath
                            | HttpLoggingFields.RequestQuery
                            | HttpLoggingFields.ResponseStatusCode
                            | HttpLoggingFields.Duration;
    options.CombineLogs = true;
});

var cabinetsFile = builder.Configuration["Archive:CabinetsFile"]
    ?? throw new InvalidOperationException("Setting 'Archive:CabinetsFile' is required");

var storagePath = builder.Configuration["Archive:StoragePath"]
    ?? throw new InvalidOperationException("Setting 'Archive:StoragePath' is required");

var maxPageSize = builder.Configuration["Archive:MaxPageSize"]
    ?? throw new InvalidOperationException("Setting 'Archive:MaxPageSize' is required");

var maxUploadBytes = builder.Configuration["Archive:MaxUploadBytes"]
    ?? throw new InvalidOperationException("Setting 'Archive:MaxUploadBytes' is required");

var maxUploadEntries = builder.Configuration["Archive:MaxUploadEntries"]
    ?? throw new InvalidOperationException("Setting 'Archive:MaxUploadEntries' is required");

var allowedOrigins = builder.Configuration["Archive:AllowedOrigins"]
    ?? throw new InvalidOperationException("Setting 'Archive:AllowedOrigins' is required");

var resolvedCabinetsFile = CabinetsServiceCollectionExtensions.ResolvePath(
    cabinetsFile, builder.Environment.ContentRootPath);

var resolvedStoragePath = CabinetsServiceCollectionExtensions.ResolvePath(
    storagePath, builder.Environment.ContentRootPath);

var uploadBytesLimit = long.Parse(maxUploadBytes, CultureInfo.InvariantCulture);

var origins = allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCabinetCatalog(cabinetsFile, builder.Environment.ContentRootPath);
builder.Services.AddDocumentsStore(storagePath, builder.Environment.ContentRootPath);
builder.Services.AddScoped<UploadPlanningService>();
builder.Services.AddScoped<UploadService>();

builder.Services.AddSingleton(new PagingLimits(int.Parse(maxPageSize, CultureInfo.InvariantCulture)));

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    policy.AllowAnyMethod()
        .AllowAnyHeader();

    if (origins.Length == 0)
    {
        policy.AllowAnyOrigin();
    }
    else
    {
        policy.WithOrigins(origins);
    }
}));

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = uploadBytesLimit;
    options.ValueCountLimit = int.Parse(maxUploadEntries, CultureInfo.InvariantCulture);
});

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = uploadBytesLimit);

var app = builder.Build();

app.Logger.LogInformation("Loaded cabinet definitions from {CabinetsFile}", resolvedCabinetsFile);
app.Logger.LogInformation("Storing documents under {StoragePath}", resolvedStoragePath);

app.UseHttpLogging();

app.UseCors();

app.UseExceptionHandler();
app.UseMiddleware<ArchiveExceptionMiddleware>();

app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Archive API v1"));

app.MapControllers();

app.Run();
