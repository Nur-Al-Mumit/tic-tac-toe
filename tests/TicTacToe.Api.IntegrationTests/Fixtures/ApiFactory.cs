using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using DotNet.Testcontainers.Images;
using Testcontainers.PostgreSql;

namespace TicTacToe.Api.IntegrationTests.Fixtures;

/// <summary>
/// One PostgreSQL 18 container per test assembly (Testcontainers). Set TICTACTOE_TEST_DB to point at an
/// existing database instead (CI or a local instance) — the container is then skipped entirely.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer? _container =
        Environment.GetEnvironmentVariable("TICTACTOE_TEST_DB") is null
            ? new PostgreSqlBuilder(new DockerImage("postgres", "18-alpine"))
                .WithDatabase("tictactoe_test")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build()
            : null;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development"); // applies migrations on startup

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _container?.GetConnectionString()
                    ?? Environment.GetEnvironmentVariable("TICTACTOE_TEST_DB"),
                ["Jwt:Key"] = "integration-test-signing-key-that-is-long-enough-0123456789",
                ["Jwt:Issuer"] = "tic-tac-toe-api",
                ["Jwt:Audience"] = "tic-tac-toe-client",
            });
        });
    }

    public Task InitializeAsync() => _container?.StartAsync() ?? Task.CompletedTask;

    public new Task DisposeAsync() => _container?.DisposeAsync().AsTask() ?? Task.CompletedTask;
}
