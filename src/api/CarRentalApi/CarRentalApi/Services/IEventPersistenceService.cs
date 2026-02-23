using ReenbitEventHub.Domain.DTOs;

namespace CarRentalApi.Services;

public interface IEventPersistenceService
{
    Task PersistAsync(EventMessage message, CancellationToken cancellationToken);
}
