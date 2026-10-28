using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Nemetschek.Archive.Api.Health;
using Nemetschek.Archive.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var cabinetsFile = builder.Configuration["Archive:CabinetsFile"]
                   ?? throw new InvalidOperationException(
                       "Setting 'Archive:CabinetsFile' is required: it is the path to cabinets.json. " +
                       "Set it in appsettings, or as the environment variable Archive__CabinetsFile.");

var resolvedCabinetsFile = CabinetsServiceCollectionExtensions.ResolvePath(
    cabinetsFile, builder.Environment.ContentRootPath);

builder.Services.AddCabinetCatalog(cabinetsFile, builder.Environment.ContentRootPath);

builder.Services.AddHealthChecks()
    .AddCheck<CabinetCatalogHealthCheck>("cabinets", tags: ["ready"]);

var app = builder.Build();

app.Logger.LogInformation("Loaded cabinet definitions from {CabinetsFile}", resolvedCabinetsFile);

app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Archive API v1"));

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.Run();