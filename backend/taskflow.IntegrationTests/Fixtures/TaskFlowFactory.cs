using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using taskFlow.Interfaces;
using taskFlow.Repositories;
using Testcontainers.PostgreSql;

namespace taskflow.IntegrationTests.Fixtures;

/// <summary>
/// Shared Postgres container that lives for the entire test collection.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("taskflow_test")
        .WithUsername("test")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    public string ConnectionString => Container.GetConnectionString();

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

/// <summary>
/// WebApplicationFactory that wires up the app against the Testcontainer Postgres.
/// </summary>
public sealed class TaskFlowFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly DatabaseFixture _db = new();

    public string ConnectionString => _db.ConnectionString;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();

        // Inject the container's connection details as env vars before the host starts
        var cs = _db.ConnectionString;
        // Parse the Npgsql connection string into individual vars the app expects
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(cs);
        Environment.SetEnvironmentVariable("DB_HOST",  builder.Host);
        Environment.SetEnvironmentVariable("DB_PORT",  (builder.Port).ToString());
        Environment.SetEnvironmentVariable("DB_NAME",  builder.Database);
        Environment.SetEnvironmentVariable("DB_USER",  builder.Username);
        Environment.SetEnvironmentVariable("DB_PASS",  builder.Password);
        Environment.SetEnvironmentVariable("JWT_SECRET", "integration-test-super-secret-key-32ch");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace the real repositories with ones pointing at the test container
            var cs = _db.ConnectionString;
            var npgsqlCs = new Npgsql.NpgsqlConnectionStringBuilder(cs);
            var connStr = $"Host={npgsqlCs.Host};Port={npgsqlCs.Port};Database={npgsqlCs.Database};Username={npgsqlCs.Username};Password={npgsqlCs.Password}";

            services.AddScoped(_ => new AuthRepository(connStr));
            services.AddScoped<IProjectService>(_ => new ProjectRepository(connStr));
        });
    }

    public new async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }
}

/// <summary>
/// xUnit collection definition — all tests in [Collection("Integration")] share one factory.
/// </summary>
[CollectionDefinition("Integration")]
public sealed class IntegrationCollection : ICollectionFixture<TaskFlowFactory> { }
