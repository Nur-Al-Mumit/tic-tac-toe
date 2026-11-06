using Scalar.AspNetCore;
using Microsoft.Extensions.Hosting;
using Serilog;
using TicTacToe.Api.Extensions;
using TicTacToe.Api.Middleware;
using TicTacToe.Api.Hubs;
using TicTacToe.Api.Realtime;
using TicTacToe.Api.Services;
using TicTacToe.Application.Contracts.Services;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Infrastructure.Data;
using TicTacToe.Application;
using TicTacToe.Infrastructure;
using TicTacToe.Infrastructure.Extensions;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext());

    builder.Services.AddControllers();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"]);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IGameNotifier, SignalRGameNotifier>();
    builder.Services.AddSignalR();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    builder.Services.AddApiRateLimiting();
    builder.Services.AddCors(options => options.AddPolicy("Client", policy => policy
        .WithOrigins((builder.Configuration["Cors:Origins"] ?? "http://localhost:4200").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));
    builder.Services.AddOpenApi();

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseCors("Client");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    // Development convenience: apply migrations on startup (CI/CD applies them as a step elsewhere).
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    }

    app.MapControllers();
    app.MapHub<GameHub>("/hubs/game");
    app.MapHealthChecks("/health");

    Log.Information("TicTacToe.Api starting");
    app.Run();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
