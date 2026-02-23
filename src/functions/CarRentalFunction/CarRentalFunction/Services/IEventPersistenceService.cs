using CarRentalFunction.Contracts;

namespace CarRentalFunction.Services;

public interface IEventPersistenceService
{
    Task PersistAsync(EventMessage message, CancellationToken cancellationToken);
}
