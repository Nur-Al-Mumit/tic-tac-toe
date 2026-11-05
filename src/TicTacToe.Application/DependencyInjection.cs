using Cortex.Mediator.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace TicTacToe.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddCortexMediator(
            new[] { typeof(DependencyInjection) },
            options => options.AddDefaultBehaviors());
        return services;
    }
}
