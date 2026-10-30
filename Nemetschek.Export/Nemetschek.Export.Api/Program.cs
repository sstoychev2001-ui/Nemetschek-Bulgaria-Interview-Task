using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpLogging;
using Nemetschek.Export.Api.Middleware;
using Nemetschek.Export.Infrastructure;

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

var archiveBaseAddress = builder.Configuration["Export:ArchiveBaseAddress"]
    ?? throw new InvalidOperationException("Setting 'Export:ArchiveBaseAddress' is required");

builder.Services.AddArchiveClient(archiveBaseAddress);

var app = builder.Build();

app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/health"),
    requests => requests.UseHttpLogging());

app.UseExceptionHandler();
app.UseMiddleware<ExportExceptionMiddleware>();

app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Export API v1"));

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.Run();
