using Cortex.Mediator.Behaviors.FluentValidation.DependencyInjection;
using Cortex.Mediator.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using TicTacToe.Application.Common.Behaviors;

namespace TicTacToe.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddFluentValidationValidators([typeof(DependencyInjection)]); // registers IValidator<T> from this assembly

        services.AddCortexMediator(
            new[] { typeof(DependencyInjection) },
            options =>
            {
                options.AddDefaultBehaviors();                    // logging behaviors
                options.AddFluentValidationBehaviors();           // ValidationCommandBehavior → 400 mapping
                options.AddOpenCommandPipelineBehavior(typeof(UnitOfWorkCommandPipelineBehavior<,>));
            });
        return services;
    }
}
