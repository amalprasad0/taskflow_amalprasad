using EvolveDb;
using Npgsql;
using taskFlow.Repositories;
using taskFlow.Interfaces;
using taskFlow.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using Dapper;

if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Testing")
{
    DotNetEnv.Env.TraversePath().Load();
}

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(isDevelopment ? LogEventLevel.Debug : LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "taskFlow")
    .Enrich.WithProperty("Environment", environment)
    .Enrich.WithProperty("MachineName", Environment.MachineName)
    .WriteTo.Console(isDevelopment
        ? (Serilog.Formatting.ITextFormatter) new Serilog.Formatting.Display.MessageTemplateTextFormatter("[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
        : new JsonFormatter())
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

var connectionString = $"Host={Environment.GetEnvironmentVariable("DB_HOST")};" +
                       $"Port={Environment.GetEnvironmentVariable("DB_PORT")};" +
                       $"Database={Environment.GetEnvironmentVariable("DB_NAME")};" +
                       $"Username={Environment.GetEnvironmentVariable("DB_USER")};" +
                       $"Password={Environment.GetEnvironmentVariable("DB_PASS")}";
Dapper.SqlMapper.AddTypeHandler(new JsonTypeHandler<Dictionary<string, int>>());
DefaultTypeMap.MatchNamesWithUnderscores = true;

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(ms => ms.Value.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => string.Join(", ", kvp.Value.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "The value is invalid." : e.ErrorMessage))
                );

            var problem = new
            {
                error = "validation failed",
                fields = errors
            };

            return new BadRequestObjectResult(problem);
        };
    });

builder.Services.AddOpenApi();

// JWT Authentication
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? builder.Configuration["Jwt:Secret"] ?? "your-secret-key";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddScoped(_ => new AuthRepository(connectionString));
builder.Services.AddScoped<IProjectService>(_ => new ProjectRepository(connectionString));

var app = builder.Build();

// Skip DB creation in integration tests — Testcontainers provides a ready-made DB.
if (!environment.Equals("Testing", StringComparison.OrdinalIgnoreCase))
    EnsureDatabaseExists();

RunMigrations(app.Configuration);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCustomExceptionHandler();
app.UseSerilogRequestLogging(opts =>
{
    opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.000}ms";
    opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? "");
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        var userId = httpContext.Items["UserId"];
        if (userId != null)
            diagnosticContext.Set("UserId", userId);
    };
});
app.UseHttpsRedirection();

app.UseJwtMiddleware();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

void EnsureDatabaseExists()
{
    var dbName = Environment.GetEnvironmentVariable("DB_NAME");

    var sysConnectionString = $"Host={Environment.GetEnvironmentVariable("DB_HOST")};" +
                           $"Port={Environment.GetEnvironmentVariable("DB_PORT")};" +
                           $"Database=postgres;" +
                           $"Username={Environment.GetEnvironmentVariable("DB_USER")};" +
                           $"Password={Environment.GetEnvironmentVariable("DB_PASS")}";

    using var connection = new NpgsqlConnection(sysConnectionString);
    connection.Open();

    using var checkCmd = new NpgsqlCommand(
        $"SELECT 1 FROM pg_database WHERE datname = '{dbName}'", connection);
    var exists = checkCmd.ExecuteScalar();

    if (exists == null)
    {
        Log.Information("Database {DbName} not found — creating...", dbName);
        using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\"", connection);
        createCmd.ExecuteNonQuery();
        Log.Information("Database {DbName} created successfully", dbName);
    }
    else
    {
        Log.Information("Database {DbName} already exists", dbName);
    }
}

void RunMigrations(IConfiguration config)
{
    try
    {
        var migrationPath = Path.Combine(AppContext.BaseDirectory, "Migrations");
        Log.Information("Looking for migrations in {MigrationPath}", migrationPath);

        if (Directory.Exists(migrationPath))
        {
            var files = Directory.GetFiles(migrationPath, "*.sql");
            Log.Information("Found {FileCount} SQL migration files", files.Length);
            foreach (var f in files)
                Log.Debug("Migration file: {FileName}", Path.GetFileName(f));
        }
        else
        {
            Log.Warning("Migrations folder NOT found at {MigrationPath}", migrationPath);
        }

        Log.Information("Starting database migrations");

        var migrConnectionString = $"Host={Environment.GetEnvironmentVariable("DB_HOST")};" +
                      $"Port={Environment.GetEnvironmentVariable("DB_PORT")};" +
                      $"Database={Environment.GetEnvironmentVariable("DB_NAME")};" +
                      $"Username={Environment.GetEnvironmentVariable("DB_USER")};" +
                      $"Password={Environment.GetEnvironmentVariable("DB_PASS")}";

        using var connection = new NpgsqlConnection(migrConnectionString);

        var evolve = new Evolve(connection, msg => Log.Debug("Evolve: {Message}", msg))
        {
            Locations = new[] { "Migrations" },
            IsEraseDisabled = true,
        };

        evolve.Migrate();
        Log.Information("Database migrations completed successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Database migration failed");
        throw;
    }
}

// Required for WebApplicationFactory in integration tests
public partial class Program { }