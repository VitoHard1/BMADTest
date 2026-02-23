using Microsoft.EntityFrameworkCore;
using ReenbitEventHub.Domain.Entities;
using ReenbitEventHub.Domain.Enums;
using ReenbitEventHub.Domain.Repositories;

namespace ReenbitEventHub.Infrastructure.Data.Repositories;

public sealed class EventRepository(EventDbContext dbContext) : IEventRepository
{
    public async Task AddAsync(Event entity, CancellationToken cancellationToken)
    {
        dbContext.Events.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyCollection<Event> Items, int TotalCount)> QueryAsync(
        string? userId,
        EventType? type,
        DateTime? from,
        DateTime? to,
        bool createdAtDescending,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // BE-02: query DB with optional filters, sorting and pagination.
        var query = dbContext.Events.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(e => e.UserId == userId);
        }

        if (type.HasValue)
        {
            query = query.Where(e => e.Type == type.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(e => e.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.CreatedAt <= to.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = createdAtDescending
            ? query.OrderByDescending(e => e.CreatedAt)
            : query.OrderBy(e => e.CreatedAt);

        var skip = (page - 1) * pageSize;
        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
