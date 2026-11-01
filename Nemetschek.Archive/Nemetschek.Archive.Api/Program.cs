using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Nemetschek.Archive.Api.Contracts.Shared;
using Nemetschek.Archive.Api.Middleware;
using Nemetschek.Archive.Infrastructure;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Services.Uploads;

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

var cabinetsFile = RequiredSetting(builder.Configuration, "Archive:CabinetsFile");

var resolvedCabinetsFile = CabinetsServiceCollectionExtensions.ResolvePath(
    cabinetsFile, builder.Environment.ContentRootPath);

var storagePath = RequiredSetting(builder.Configuration, "Archive:StoragePath");

var resolvedStoragePath = CabinetsServiceCollectionExtensions.ResolvePath(
    storagePath, builder.Environment.ContentRootPath);

builder.Services.AddCabinetCatalog(cabinetsFile, builder.Environment.ContentRootPath);
builder.Services.AddDocumentsStore(storagePath, builder.Environment.ContentRootPath);
builder.Services.AddScoped<UploadPlanningService>();
builder.Services.AddScoped<UploadService>();

builder.Services.AddSingleton(
    new PagingLimits(builder.Configuration.GetValue("Archive:MaxPageSize", 50)));

var allowedOrigins = (builder.Configuration["Archive:AllowedOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    policy.AllowAnyMethod()
        .AllowAnyHeader();

    if (allowedOrigins.Length == 0)
    {
        policy.AllowAnyOrigin();
    }
    else
    {
        policy.WithOrigins(allowedOrigins);
    }
}));

var maxUploadBytes = builder.Configuration.GetValue("Archive:MaxUploadBytes", 256L * 1024 * 1024);
var maxUploadEntries = builder.Configuration.GetValue("Archive:MaxUploadEntries", 10_000);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxUploadBytes;
    options.ValueCountLimit = maxUploadEntries;
});

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxUploadBytes);

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

// appsettings.json lists every setting the service reads, the required ones as empty placeholders
// that show the expected type. An empty placeholder therefore means unset, exactly as a missing key
// does, and both have to fail here naming the setting rather than further in.
static string RequiredSetting(IConfiguration configuration, string key)
{
    var value = configuration[key];

    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Setting '{key}' is required");
    }

    return value;
}