using System.Text;
using Cheferee.Api.Configuration;
using Cheferee.Api.Endpoints;
using Cheferee.Infrastructure.Configuration;
using Cheferee.Infrastructure.Data;
using Cheferee.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

    // 1. Strongly-typed settings validation
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

    builder.Services.AddOptions<JwtSettings>()
        .Bind(builder.Configuration.GetSection("Jwt"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    // 2. Infrastructure Options bindings
    builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

    // 3. Database Context
    var dbSettings = builder.Configuration.GetSection("Database").Get<DatabaseSettings>()
        ?? throw new InvalidOperationException("Database settings missing.");
    builder.Services.AddDbContext<ChefereeDbContext>(options =>
    {
        options.UseNpgsql(dbSettings.ConnectionString);
    });

    // 4. Infrastructure Services
    builder.Services.AddSingleton<ITokenService, TokenService>();
    builder.Services.AddSingleton<IStorageService, LocalStorageService>();

    // 5. Authentication & JWT Bearer
    var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
        ?? throw new InvalidOperationException("Jwt settings missing.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });

    builder.Services.AddAuthorization();

    // 6. CORS Policy for Next.js frontend
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

    // Enable static media serving (e.g., http://localhost:5000/media/{filename})
    app.UseStaticFiles();

    app.UseAuthentication();
    app.UseAuthorization();

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

    // 7. Route Groups
    app.MapGroup("/api/v1/auth").MapAuthEndpoints();
    app.MapGroup("/api/v1/users").MapUserEndpoints();
    app.MapGroup("/api/v1/posts").MapPostEndpoints();

    app.Run();
}
catch (HostAbortedException)
{
    // CLI tooling inspection (e.g. dotnet ef)
}
catch (Exception ex)
{
    Log.Fatal(ex, "Cheferee backend host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}