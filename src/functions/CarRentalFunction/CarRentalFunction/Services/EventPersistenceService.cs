using CarRentalFunction.Contracts;
using CarRentalFunction.Data;
using CarRentalFunction.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarRentalFunction.Services;

public sealed class EventPersistenceService(
    EventDbContext dbContext,
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
            dbContext.Events.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Persisted event {EventId} to database", message.Id);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            logger.LogInformation("Duplicate event {EventId} ignored (already exists)", message.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist event {EventId}", message.Id);
            throw;
        }
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }
}
