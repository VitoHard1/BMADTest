using ReenbitEventHub.Domain.DTOs;

namespace ReenbitEventHub.Application.Services;

public interface IMessagePublisher
{
    Task<IReadOnlyList<Guid>> PublishEventsAsync(
        IReadOnlyList<EventMessage> events,
        CancellationToken cancellationToken = default);
}
