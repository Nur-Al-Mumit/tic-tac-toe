using Scalar.AspNetCore;
using Serilog;
using TicTacToe.Api.Middleware;
using TicTacToe.Application;
using TicTacToe.Infrastructure;
using TicTacToe.Infrastructure.Persistence;

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
        .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);
    builder.Services.AddCors(options => options.AddPolicy("Client", policy => policy
        .WithOrigins((builder.Configuration["Cors:Origins"] ?? "http://localhost:4200").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));
    builder.Services.AddOpenApi();

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseCors("Client");
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.MapControllers();
    app.MapHealthChecks("/health");

    Log.Information("TicTacToe.Api starting");
    app.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
