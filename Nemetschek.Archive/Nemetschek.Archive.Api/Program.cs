using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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

var cabinetsFile = builder.Configuration["Archive:CabinetsFile"]
    ?? throw new InvalidOperationException("Setting 'Archive:CabinetsFile' is required");

var resolvedCabinetsFile = CabinetsServiceCollectionExtensions.ResolvePath(
    cabinetsFile, builder.Environment.ContentRootPath);

var storagePath = builder.Configuration["Archive:StoragePath"]
    ?? throw new InvalidOperationException("Setting 'Archive:StoragePath' is required");

var resolvedStoragePath = CabinetsServiceCollectionExtensions.ResolvePath(
    storagePath, builder.Environment.ContentRootPath);

builder.Services.AddCabinetCatalog(cabinetsFile, builder.Environment.ContentRootPath);
builder.Services.AddDocumentsStore(storagePath, builder.Environment.ContentRootPath);
builder.Services.AddScoped<UploadPlanningService>();
builder.Services.AddScoped<UploadService>();

builder.Services.AddSingleton(
    new PagingLimits(builder.Configuration.GetValue("Archive:MaxPageSize", 50)));

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

app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/health"),
    requests => requests.UseHttpLogging());

app.UseExceptionHandler();
app.UseMiddleware<ArchiveExceptionMiddleware>();

app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Archive API v1"));

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.Run();