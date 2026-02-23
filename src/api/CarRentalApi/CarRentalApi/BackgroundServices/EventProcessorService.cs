using System.Threading.Channels;
using CarRentalApi.Services;
using ReenbitEventHub.Domain.DTOs;

namespace CarRentalApi.BackgroundServices;

public sealed class EventProcessorService(
    Channel<EventMessage> channel,
    IServiceScopeFactory scopeFactory,
    ILogger<EventProcessorService> logger) : BackgroundService
{
    private const int MaxAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in channel.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessWithRetryAsync(message, stoppingToken);
        }
    }

    private async Task ProcessWithRetryAsync(EventMessage message, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                logger.LogInformation(
                    "Received message {MessageId} | eventId={EventId} userId={UserId} type={EventType} attempt={Attempt}",
                    message.Id,
                    message.Id,
                    message.UserId,
                    message.Type,
                    attempt);

                using var scope = scopeFactory.CreateScope();
                var persistenceService = scope.ServiceProvider.GetRequiredService<IEventPersistenceService>();
                await persistenceService.PersistAsync(message, cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && !cancellationToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(attempt);
                logger.LogWarning(
                    ex,
                    "Failed persisting event {EventId} on attempt {Attempt}; retrying in {DelaySeconds}s",
                    message.Id,
                    attempt,
                    delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException($"Failed to persist event {message.Id} after {MaxAttempts} attempts.");
    }
}
