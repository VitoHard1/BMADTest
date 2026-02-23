using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using ReenbitEventHub.Application.Exceptions;
using ReenbitEventHub.Application.Services;
using ReenbitEventHub.Domain.DTOs;

namespace ReenbitEventHub.Infrastructure.Messaging;

public sealed class ServiceBusPublisher(
    ServiceBusSender sender,
    ILogger<ServiceBusPublisher> logger) : IMessagePublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<IReadOnlyList<Guid>> PublishEventsAsync(
        IReadOnlyList<EventMessage> events,
        CancellationToken cancellationToken = default)
    {
        foreach (var evt in events)
        {
            var json = JsonSerializer.Serialize(evt, JsonOptions);
            var message = new ServiceBusMessage(json)
            {
                MessageId = evt.Id.ToString(),
                ContentType = "application/json"
            };

            try
            {
                await sender.SendMessageAsync(message, cancellationToken);
                logger.LogInformation(
                    "Published event {EventId} userId={UserId} type={EventType}",
                    evt.Id, evt.UserId, evt.Type);
            }
            catch (ServiceBusException ex)
            {
                throw new MessagePublishException(
                    $"Failed to publish event {evt.Id} to Service Bus queue.", ex);
            }
        }

        return events.Select(e => e.Id).ToList();
    }
}
