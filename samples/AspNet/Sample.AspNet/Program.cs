using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting web application");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, loggerConfig) =>
        loggerConfig.ReadFrom.Configuration(context.Configuration));

    var app = builder.Build();

    app.MapGet("/log-debug", (ILogger<Program> logger) => { logger.LogDebug("Debug from sample"); })
        .WithName("log-debug");

    app.MapGet("/log-info", (ILogger<Program> logger) => { logger.LogInformation("Information from sample"); })
        .WithName("log-info");

    app.MapGet("/log-warning", (ILogger<Program> logger) => { logger.LogWarning("Warning from sample"); })
        .WithName("log-warning");

    app.MapGet("/log-error", (ILogger<Program> logger) => { logger.LogError("Error from sample"); })
        .WithName("log-error");

    app.MapGet("/log-fatal",
            (ILogger<Program> logger) =>
            {
                logger.LogCritical("Fatal from sample {Exception}", new Exception("Sample exception"));
            })
        .WithName("log-fatal");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}