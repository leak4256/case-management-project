using System.Diagnostics;
using System.Text.Json.Serialization;
using CaseManagement.Api.ExceptionHandling;
using CaseManagement.Api.Extensions;
using CaseManagement.Application;
using CaseManagement.Application.DTOs;
using CaseManagement.Infrastructure;
using Microsoft.Net.Http.Headers;

const string CorsPolicyName = "AngularClient";

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

        // A deserializer message names the CLR type it failed to bind to, and unlike the
        // exception handler it is not gated on the environment.
        options.AllowInputFormatterExceptionMessages = false;
    });

builder.Services.AddHealthChecks();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    // A cross-origin response hides every header not listed here, and GET /cases/{id} carries
    // its concurrency token in the ETag header.
    .WithExposedHeaders(HeaderNames.ETag)));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance = context.HttpContext.Request.Path;

    context.ProblemDetails.Extensions["traceId"] =
        Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddSwaggerGen(options =>
{
    foreach (var assembly in new[] { typeof(Program).Assembly, typeof(CaseDto).Assembly })
    {
        var documentationPath = Path.Combine(
            AppContext.BaseDirectory,
            $"{assembly.GetName().Name}.xml");

        if (File.Exists(documentationPath))
        {
            options.IncludeXmlComments(documentationPath);
        }
    }
});

var app = builder.Build();

if (allowedOrigins.Length == 0)
{
    app.Logger.LogWarning("No CORS origins are configured; browser clients will be blocked.");
}
else
{
    app.Logger.LogInformation("CORS allows origins: {AllowedOrigins}.", allowedOrigins);
}

await app.InitializeDatabaseAsync();

app.UseExceptionHandler();

// Without this a bare 404 or 405 carries no body, leaving the client two error shapes to parse.
app.UseStatusCodePages();

app.UseCors(CorsPolicyName);

// Served in every environment on purpose: in the container ASPNETCORE_ENVIRONMENT is Production,
// and gating this on Development would hide the only way the API gets explored.
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Lets the integration tests reach the real application through WebApplicationFactory<Program>.
public partial class Program;
