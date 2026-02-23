using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using CarRentalFunction.Contracts;
using CarRentalFunction.Services;

namespace CarRentalFunction;

public sealed class EventProcessorFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IEventPersistenceService _eventPersistenceService;
    private readonly ILogger<EventProcessorFunction> _logger;

    public EventProcessorFunction(
        IEventPersistenceService eventPersistenceService,
        ILogger<EventProcessorFunction> logger)
    {
        _eventPersistenceService = eventPersistenceService;
        _logger = logger;
    }

    [Function(nameof(EventProcessorFunction))]
    public async Task Run(
        [ServiceBusTrigger("%ServiceBusQueueName%", Connection = "ServiceBus__ConnectionString")]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = message.Body.ToString();
            var eventMessage = JsonSerializer.Deserialize<EventMessage>(payload, JsonOptions);
            if (eventMessage is null)
            {
                throw new JsonException($"Unable to deserialize message {message.MessageId} to EventMessage.");
            }

            _logger.LogInformation(
                "Received message {MessageId} | eventId={EventId} userId={UserId} type={EventType}",
                message.MessageId,
                eventMessage.Id,
                eventMessage.UserId,
                eventMessage.Type);

            await _eventPersistenceService.PersistAsync(eventMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed processing message {MessageId}; allowing retry", message.MessageId);
            throw;
        }
    }
}
