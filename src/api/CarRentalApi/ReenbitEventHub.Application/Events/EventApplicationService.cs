using Microsoft.Extensions.Logging;
using ReenbitEventHub.Application.Services;
using ReenbitEventHub.Domain.Constants;
using ReenbitEventHub.Domain.DTOs;
using ReenbitEventHub.Domain.Entities;
using ReenbitEventHub.Domain.Enums;
using ReenbitEventHub.Domain.Repositories;

namespace ReenbitEventHub.Application.Events;

public sealed class EventApplicationService(
    IEventRepository repository,
    IMessagePublisher publisher,
    ILogger<EventApplicationService> logger) : IEventApplicationService
{
    public async Task<CreateEventResponse> CreateAsync(CreateEventRequest request, CancellationToken cancellationToken)
    {
        Validate(request);

        var userId = request.UserId.Trim();
        var carId = request.CarId.Trim().ToLowerInvariant();
        var carName = CarCatalog.GetCarName(carId);
        var now = DateTime.UtcNow;

        var events = request.Action switch
        {
            EventAction.ViewCar => new[]
            {
                Event.Create(userId, EventType.PageView,
                    request.Description?.Trim() ?? $"Viewed {carId} {carName}", now)
            },
            EventAction.ReserveCar => new[]
            {
                Event.Create(userId, EventType.Click,
                    request.Description?.Trim() ?? $"Clicked reserve for {carId} {carName}", now),
                Event.Create(userId, EventType.Purchase,
                    $"Reserved {carId} {carName}", now)
            },
            _ => throw new ArgumentException("Action is invalid.", nameof(request.Action))
        };

        logger.LogInformation("Publishing {EventCount} events for user {UserId}", events.Length, userId);

        var messages = events.Select(e => new EventMessage
        {
            Id = e.Id,
            UserId = e.UserId,
            Type = e.Type,
            Description = e.Description,
            CreatedAt = e.CreatedAt
        }).ToList();

        await publisher.PublishEventsAsync(messages, cancellationToken);

        return new CreateEventResponse
        {
            PublishedCount = events.Length,
            EventIds = events.Select(e => e.Id).ToList()
        };
    }

    public async Task<GetEventsResponse> GetAsync(GetEventsQueryRequest query, CancellationToken cancellationToken)
    {
        query ??= new GetEventsQueryRequest();
        Validate(query);

        var createdAtDescending = !string.Equals(query.Sort, "createdAt_asc", StringComparison.OrdinalIgnoreCase);
        var page = query.Page;
        var pageSize = query.PageSize;

        var (items, totalCount) = await repository.QueryAsync(
            string.IsNullOrWhiteSpace(query.UserId) ? null : query.UserId.Trim(),
            query.Type,
            query.From,
            query.To,
            createdAtDescending,
            page,
            pageSize,
            cancellationToken);

        return new GetEventsResponse
        {
            Items = items.Select(ToResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private static EventResponse ToResponse(Event entity) =>
        new()
        {
            Id = entity.Id,
            UserId = entity.UserId,
            Type = entity.Type,
            Description = entity.Description,
            CreatedAt = entity.CreatedAt
        };

    private static void Validate(CreateEventRequest request)
    {
        if (request is null)
            throw new ArgumentException("Request is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.UserId))
            throw new ArgumentException("UserId is required.", nameof(request.UserId));

        if (request.UserId.Length > 100)
            throw new ArgumentException("UserId length must be 100 or less.", nameof(request.UserId));

        if (!Enum.IsDefined(request.Action))
            throw new ArgumentException("Action must be ViewCar or ReserveCar.", nameof(request.Action));

        if (string.IsNullOrWhiteSpace(request.CarId) || !CarCatalog.IsValidCarId(request.CarId.Trim().ToLowerInvariant()))
            throw new ArgumentException("CarId must be 'car-1' or 'car-2'.", nameof(request.CarId));

        if (request.Description is not null && request.Description.Length > 500)
            throw new ArgumentException("Description length must be 500 or less.", nameof(request.Description));
    }

    private static void Validate(GetEventsQueryRequest query)
    {
        if (query.Page < 1)
            throw new ArgumentException("Page must be greater than or equal to 1.", nameof(query.Page));

        if (query.PageSize < 1 || query.PageSize > 200)
            throw new ArgumentException("PageSize must be between 1 and 200.", nameof(query.PageSize));

        if (string.IsNullOrWhiteSpace(query.Sort)
            || (!string.Equals(query.Sort, "createdAt_desc", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(query.Sort, "createdAt_asc", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Sort must be 'createdAt_desc' or 'createdAt_asc'.", nameof(query.Sort));
        }

        if (query.UserId is not null && string.IsNullOrWhiteSpace(query.UserId))
            throw new ArgumentException("UserId cannot be empty when provided.", nameof(query.UserId));

        if (query.From.HasValue && query.To.HasValue && query.From.Value > query.To.Value)
            throw new ArgumentException("From must be less than or equal to To.", nameof(query.From));
    }
}
