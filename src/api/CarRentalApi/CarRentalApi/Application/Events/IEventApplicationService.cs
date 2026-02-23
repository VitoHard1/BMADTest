using ReenbitEventHub.Domain.Entities;

namespace CarRentalApi.Application.Events;

public interface IEventApplicationService
{
    Task<Event> CreateAsync(CreateEventCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Event>> GetAllAsync(CancellationToken cancellationToken);
}
