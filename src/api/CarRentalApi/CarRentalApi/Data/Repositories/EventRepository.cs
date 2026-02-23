using Microsoft.EntityFrameworkCore;
using ReenbitEventHub.Domain.Entities;
using ReenbitEventHub.Domain.Repositories;

namespace CarRentalApi.Data.Repositories;

public sealed class EventRepository(EventDbContext dbContext) : IEventRepository
{
    public async Task AddAsync(Event entity, CancellationToken cancellationToken)
    {
        dbContext.Events.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Event>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Events
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
