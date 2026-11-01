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

var archiveBaseAddress = RequiredSetting(builder.Configuration, "Export:ArchiveBaseAddress");

builder.Services.AddArchiveClient(archiveBaseAddress);

var allowedOrigins = (builder.Configuration["Export:AllowedOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    policy.AllowAnyMethod()
        .AllowAnyHeader()
        .WithExposedHeaders("Content-Disposition");

    if (allowedOrigins.Length == 0)
    {
        policy.AllowAnyOrigin();
    }
    else
    {
        policy.WithOrigins(allowedOrigins);
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

// appsettings.json lists every setting the service reads, the required ones as empty placeholders
// that show the expected type. An empty placeholder therefore means unset, exactly as a missing key
// does, and both have to fail here naming the setting rather than at the first export.
static string RequiredSetting(IConfiguration configuration, string key)
{
    var value = configuration[key];

    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Setting '{key}' is required");
    }

    return value;
}
