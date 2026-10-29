using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Nemetschek.Archive.Api;
using Nemetschek.Archive.Api.Health;
using Nemetschek.Archive.Api.Middleware;
using Nemetschek.Archive.Core.Domain.Uploads;
using Nemetschek.Archive.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// No endpoint chooses a status code, so the framework must not choose one either: without this, a
// blank 'field' or 'folderName' would come back as an automatic 400 while every other refusal came
// back as 404 from the middleware. Suppressed, those two reach the rules and are refused by name
// in the report like anything else. The DataAnnotations on UploadFormRequest stay, for OpenAPI.
builder.Services.Configure<ApiBehaviorOptions>(
    options => options.SuppressModelStateInvalidFilter = true);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var cabinetsFile = builder.Configuration["Archive:CabinetsFile"]
                   ?? throw new InvalidOperationException(
                       "Setting 'Archive:CabinetsFile' is required: it is the path to cabinets.json. " +
                       "Set it in appsettings, or as the environment variable Archive__CabinetsFile.");

var resolvedCabinetsFile = CabinetsServiceCollectionExtensions.ResolvePath(
    cabinetsFile, builder.Environment.ContentRootPath);

// Required and with no default, like the cabinets file: the same image has to run with different
// settings, and a default would hide a deployment that forgot to mount a volume — which would then
// store documents into the container's own layer and lose them on the next restart.
var storagePath = builder.Configuration["Archive:StoragePath"]
                  ?? throw new InvalidOperationException(
                      "Setting 'Archive:StoragePath' is required: it is the directory the Archive " +
                      "keeps documents in, and it must survive a container restart. Set it in " +
                      "appsettings, or as the environment variable Archive__StoragePath.");

var resolvedStoragePath = CabinetsServiceCollectionExtensions.ResolvePath(
    storagePath, builder.Environment.ContentRootPath);

builder.Services.AddCabinetCatalog(cabinetsFile, builder.Environment.ContentRootPath);
builder.Services.AddFileSystemDocumentStore(storagePath, builder.Environment.ContentRootPath);
builder.Services.AddScoped<UploadPlanningService>();
builder.Services.AddScoped<UploadService>();

builder.Services.AddSingleton(
    new PagingLimits(builder.Configuration.GetValue("Archive:MaxPageSize", 50)));

// Everything a user uploads is untrusted, so the size of a body and the number of parts in it are
// bounded before any of it is read. Both have defaults so the service runs out of the box, and
// both are settings because the same image has to run with different ones.
var maxUploadBytes = builder.Configuration.GetValue("Archive:MaxUploadBytes", 256L * 1024 * 1024);
var maxUploadEntries = builder.Configuration.GetValue("Archive:MaxUploadEntries", 10_000);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxUploadBytes;
    options.ValueCountLimit = maxUploadEntries;
});

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxUploadBytes);

builder.Services.AddHealthChecks()
    .AddCheck<CabinetCatalogHealthCheck>("cabinets", tags: ["ready"])
    .AddCheck<DocumentStorageHealthCheck>("storage", tags: ["ready"]);

var app = builder.Build();

app.Logger.LogInformation("Loaded cabinet definitions from {CabinetsFile}", resolvedCabinetsFile);
app.Logger.LogInformation("Storing documents under {StoragePath}", resolvedStoragePath);

app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Archive API v1"));

// Before the endpoints, so it is still in the response's way when the action returns. This is the
// only thing in the service that turns an outcome into a status code.
app.UseMiddleware<UploadFailureStatusMiddleware>();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.Run();
