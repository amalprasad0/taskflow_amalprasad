using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using taskFlow.Interfaces;
using taskFlow.Repositories;
using Testcontainers.PostgreSql;

namespace taskflow.IntegrationTests.Fixtures;

public sealed class TaskFlowFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("taskflow_test")
        .WithUsername("test")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        
        var cs = new Npgsql.NpgsqlConnectionStringBuilder(_postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("DB_HOST",  cs.Host);
        Environment.SetEnvironmentVariable("DB_PORT",  cs.Port.ToString());
        Environment.SetEnvironmentVariable("DB_NAME",  cs.Database);
        Environment.SetEnvironmentVariable("DB_USER",  cs.Username);
        Environment.SetEnvironmentVariable("DB_PASS",  cs.Password);
        Environment.SetEnvironmentVariable("JWT_SECRET", "integration-test-super-secret-key-32ch");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
    }

    
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var cs = new Npgsql.NpgsqlConnectionStringBuilder(_postgres.GetConnectionString());
            var connStr = $"Host={cs.Host};Port={cs.Port};Database={cs.Database};Username={cs.Username};Password={cs.Password}";

            
            services.AddScoped(_ => new AuthRepository(connStr));
            services.AddScoped<IProjectService>(_ => new ProjectRepository(connStr));
        });
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition("Integration")]
public sealed class IntegrationCollection : ICollectionFixture<TaskFlowFactory> { }
