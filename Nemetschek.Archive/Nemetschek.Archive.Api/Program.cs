using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Nemetschek.Archive.Api.Health;
using Nemetschek.Archive.Core.Domain.Uploads;
using Nemetschek.Archive.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var cabinetsFile = builder.Configuration["Archive:CabinetsFile"]
                   ?? throw new InvalidOperationException(
                       "Setting 'Archive:CabinetsFile' is required: it is the path to cabinets.json. " +
                       "Set it in appsettings, or as the environment variable Archive__CabinetsFile.");

var resolvedCabinetsFile = CabinetsServiceCollectionExtensions.ResolvePath(
    cabinetsFile, builder.Environment.ContentRootPath);

builder.Services.AddCabinetCatalog(cabinetsFile, builder.Environment.ContentRootPath);
builder.Services.AddInMemoryDocumentStore();
builder.Services.AddScoped<UploadService>();

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
    .AddCheck<CabinetCatalogHealthCheck>("cabinets", tags: ["ready"]);

var app = builder.Build();

app.Logger.LogInformation("Loaded cabinet definitions from {CabinetsFile}", resolvedCabinetsFile);

app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Archive API v1"));

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.Run();
