using EvolveDb;
using Npgsql;
using taskFlow.Repositories;

DotNetEnv.Env.Load();
var builder = WebApplication.CreateBuilder(args);

var connectionString = $"Host={Environment.GetEnvironmentVariable("DB_HOST")};" +
                       $"Port={Environment.GetEnvironmentVariable("DB_PORT")};" +
                       $"Database={Environment.GetEnvironmentVariable("DB_NAME")};" +
                       $"Username={Environment.GetEnvironmentVariable("DB_USER")};" +
                       $"Password={Environment.GetEnvironmentVariable("DB_PASS")}";

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddScoped(_ => new AuthRepository(connectionString));

var app = builder.Build();
EnsureDatabaseExists();
RunMigrations(app.Configuration);
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

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