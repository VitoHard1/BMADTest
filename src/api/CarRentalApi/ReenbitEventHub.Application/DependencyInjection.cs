using Microsoft.Extensions.DependencyInjection;
using ReenbitEventHub.Application.Events;

namespace ReenbitEventHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IEventApplicationService, EventApplicationService>();
        return services;
    }
}
