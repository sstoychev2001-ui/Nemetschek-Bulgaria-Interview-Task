var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

// The brief requires the OpenAPI description to be browsable while the service runs in its
// container, so this is deliberately not gated on the Development environment.
app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Archive API v1"));

// Services may start in any order and may be briefly unavailable, so compose needs something
// cheap to poll that does not touch storage.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("Health")
   .WithSummary("Liveness probe.");

app.Run();
