using Microsoft.EntityFrameworkCore;
using ReenbitEventHub.Domain.DTOs;
using ReenbitEventHub.Domain.Entities;
using ReenbitEventHub.Domain.Repositories;

namespace CarRentalApi.Services;

public sealed class EventPersistenceService(
    IEventRepository repository,
    ILogger<EventPersistenceService> logger) : IEventPersistenceService
{
    public async Task PersistAsync(EventMessage message, CancellationToken cancellationToken)
    {
        var entity = Event.Reconstitute(
            message.Id,
            message.UserId,
            message.Type,
            message.Description,
            message.CreatedAt);

        try
        {
            await repository.AddAsync(entity, cancellationToken);
            logger.LogInformation("Persisted event {EventId} to database", message.Id);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            logger.LogInformation("Duplicate event {EventId} ignored (already exists)", message.Id);
        }
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }
}
