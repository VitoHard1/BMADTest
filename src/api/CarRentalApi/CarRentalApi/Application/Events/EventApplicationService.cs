using ReenbitEventHub.Domain.Entities;
using ReenbitEventHub.Domain.Repositories;

namespace CarRentalApi.Application.Events;

public sealed class EventApplicationService(IEventRepository repository) : IEventApplicationService
{
    public async Task<Event> CreateAsync(CreateEventCommand command, CancellationToken cancellationToken)
    {
        var entity = new Event
        {
            Id = Guid.NewGuid(),
            UserId = command.UserId,
            Type = command.Type,
            Description = command.Description,
            CreatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task<IReadOnlyCollection<Event>> GetAllAsync(CancellationToken cancellationToken)
    {
        return repository.GetAllAsync(cancellationToken);
    }
}
