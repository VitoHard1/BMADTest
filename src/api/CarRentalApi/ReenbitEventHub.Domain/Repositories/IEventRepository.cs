using ReenbitEventHub.Domain.Entities;
using ReenbitEventHub.Domain.Enums;

namespace ReenbitEventHub.Domain.Repositories;

public interface IEventRepository
{
    Task AddAsync(Event entity, CancellationToken cancellationToken);
    Task<(IReadOnlyCollection<Event> Items, int TotalCount)> QueryAsync(
        string? userId,
        EventType? type,
        DateTime? from,
        DateTime? to,
        bool createdAtDescending,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
