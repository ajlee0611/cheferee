using Cheferee.Api.Configuration;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddOptions<DatabaseSettings>()
        .Bind(builder.Configuration.GetSection("Database"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<RedisSettings>()
        .Bind(builder.Configuration.GetSection("Redis"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<StorageSettings>()
        .Bind(builder.Configuration.GetSection("Storage"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("FrontendPolicy", policy =>
        {
            policy.WithOrigins("http://localhost:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });

    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseCors("FrontendPolicy");

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.MapHealthChecks("/healthz");

    app.MapGet("/api/v1/ping", () => Results.Ok(new
    {
        Status = "Match Ready",
        Timestamp = DateTimeOffset.UtcNow,
        Service = "Cheferee.Api"
    }))
    .WithName("Ping");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Cheferee backend host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}