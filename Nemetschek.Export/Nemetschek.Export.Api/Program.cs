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

var allowedOrigins = builder.Configuration["Export:AllowedOrigins"]
    ?? throw new InvalidOperationException("Setting 'Export:AllowedOrigins' is required");

var origins = allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddArchiveClient(archiveBaseAddress);

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    policy.AllowAnyMethod()
        .AllowAnyHeader()
        .WithExposedHeaders("Content-Disposition");

    if (origins.Length == 0)
    {
        policy.AllowAnyOrigin();
    }
    else
    {
        policy.WithOrigins(origins);
    }
}));

var app = builder.Build();

app.UseHttpLogging();

app.UseCors();

app.UseExceptionHandler();
app.UseMiddleware<ExportExceptionMiddleware>();

app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Export API v1"));

app.MapControllers();

app.Run();
