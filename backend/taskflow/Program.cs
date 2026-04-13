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
using Serilog.Formatting.Json;

DotNetEnv.Env.Load();

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(new JsonFormatter())
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

var connectionString = $"Host={Environment.GetEnvironmentVariable("DB_HOST")};" +
                       $"Port={Environment.GetEnvironmentVariable("DB_PORT")};" +
                       $"Database={Environment.GetEnvironmentVariable("DB_NAME")};" +
                       $"Username={Environment.GetEnvironmentVariable("DB_USER")};" +
                       $"Password={Environment.GetEnvironmentVariable("DB_PASS")}";

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
EnsureDatabaseExists();
RunMigrations(app.Configuration);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCustomExceptionHandler();
app.UseSerilogRequestLogging();
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

    // Connect to the default 'postgres' database to check/create
    var connectionString = $"Host={Environment.GetEnvironmentVariable("DB_HOST")};" +
                           $"Port={Environment.GetEnvironmentVariable("DB_PORT")};" +
                           $"Database=postgres;" +  // ← always exists
                           $"Username={Environment.GetEnvironmentVariable("DB_USER")};" +
                           $"Password={Environment.GetEnvironmentVariable("DB_PASS")}";

    using var connection = new NpgsqlConnection(connectionString);
    connection.Open();

    // Check if database exists
    using var checkCmd = new NpgsqlCommand(
        $"SELECT 1 FROM pg_database WHERE datname = '{dbName}'", connection);

    var exists = checkCmd.ExecuteScalar();

    if (exists == null)
    {
        Console.WriteLine($"📦 Database '{dbName}' not found — creating...");
        using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\"", connection);
        createCmd.ExecuteNonQuery();
        Console.WriteLine($"✅ Database '{dbName}' created successfully!");
    }
    else
    {
        Console.WriteLine($"✅ Database '{dbName}' already exists.");
    }
}

void RunMigrations(IConfiguration config)
{
    try
    {
            var migrationPath = Path.Combine(AppContext.BaseDirectory, "Migrations");
    Console.WriteLine($"Looking for migrations in: {migrationPath}");
 if (Directory.Exists(migrationPath))
    {
        var files = Directory.GetFiles(migrationPath, "*.sql");
        Console.WriteLine($"Found {files.Length} SQL files:");
        foreach (var f in files)
            Console.WriteLine($"  - {Path.GetFileName(f)}");
    }
    else
    {
        Console.WriteLine("❌ Migrations folder NOT found in output!");
    }
        Console.WriteLine("Starting database migrations...");
        var connectionString = $"Host={Environment.GetEnvironmentVariable("DB_HOST")};" +
                      $"Port={Environment.GetEnvironmentVariable("DB_PORT")};" +
                      $"Database={Environment.GetEnvironmentVariable("DB_NAME")};" +
                      $"Username={Environment.GetEnvironmentVariable("DB_USER")};" +
                      $"Password={Environment.GetEnvironmentVariable("DB_PASS")}";
        // var connectionString = config.GetConnectionString("DefaultConnection");

        using var connection = new NpgsqlConnection(connectionString);

        var evolve = new Evolve(connection, msg => Console.WriteLine(msg))
        {
            Locations = new[] { "Migrations" },
            IsEraseDisabled = true,
        };

        evolve.Migrate();
        Console.WriteLine("Database migrations completed successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration failed: {ex.Message}");
        Environment.Exit(1); // Exit with error code
    }

}