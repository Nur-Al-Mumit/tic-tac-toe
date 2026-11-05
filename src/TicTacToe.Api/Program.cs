using Scalar.AspNetCore;
using Serilog;
using TicTacToe.Api.Middleware;
using TicTacToe.Application;
using TicTacToe.Infrastructure;

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
    builder.Services.AddInfrastructure();
    builder.Services.AddHealthChecks();
    builder.Services.AddOpenApi();

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();
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
