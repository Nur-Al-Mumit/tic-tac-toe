using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicTacToe.Infrastructure.Data;

namespace TicTacToe.Infrastructure.Extensions;

public static class DbContextExtension
{
    public static void AddDbContext(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention()
                   .EnableSensitiveDataLogging(Environment.GetEnvironmentVariable("TTT_EF_SENSITIVE_LOG") == "1"));
    }
}
