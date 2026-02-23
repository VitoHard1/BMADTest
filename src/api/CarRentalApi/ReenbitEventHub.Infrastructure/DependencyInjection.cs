using System.Threading.Channels;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReenbitEventHub.Application.Services;
using ReenbitEventHub.Domain.DTOs;
using ReenbitEventHub.Domain.Repositories;
using ReenbitEventHub.Infrastructure.Data;
using ReenbitEventHub.Infrastructure.Data.Repositories;
using ReenbitEventHub.Infrastructure.Messaging;

namespace ReenbitEventHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database — uses SQL Server when the connection string contains "Server=", SQLite otherwise
        var eventsDbConnection = configuration.GetConnectionString("EventsDb") ?? "Data Source=events.db";
        services.AddDbContext<EventDbContext>(options =>
        {
            if (eventsDbConnection.Contains("Server=", StringComparison.OrdinalIgnoreCase))
                options.UseSqlServer(eventsDbConnection);
            else
                options.UseSqlite(eventsDbConnection);
        });
        services.AddScoped<IEventRepository, EventRepository>();

        // Messaging — real Azure Service Bus when connection string is present, in-memory channel otherwise
        var serviceBusConnectionString = configuration["ServiceBus:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(serviceBusConnectionString))
        {
            var queueName = configuration["ServiceBus:QueueName"];
            var entityName = !string.IsNullOrWhiteSpace(queueName) ? queueName : "events";

            services.AddSingleton(new ServiceBusClient(serviceBusConnectionString));
            services.AddSingleton(sp =>
                sp.GetRequiredService<ServiceBusClient>().CreateSender(entityName));
            services.AddScoped<IMessagePublisher, ServiceBusPublisher>();
        }
        else
        {
            services.AddSingleton(Channel.CreateUnbounded<EventMessage>(
                new UnboundedChannelOptions { SingleReader = true }));
            services.AddScoped<IMessagePublisher, InMemoryPublisher>();
        }

        return services;
    }

    public static async Task InitializeInfrastructureAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventDbContext>();

        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pendingMigrations.Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
            return;
        }

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }
}
