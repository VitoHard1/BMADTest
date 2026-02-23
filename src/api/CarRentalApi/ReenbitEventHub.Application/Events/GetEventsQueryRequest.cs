using ReenbitEventHub.Domain.Enums;

namespace ReenbitEventHub.Application.Events;

// BE-02: Query parameters contract for GET /api/events.
public sealed class GetEventsQueryRequest
{
    public string? UserId { get; init; }
    public EventType? Type { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string Sort { get; init; } = "createdAt_desc";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
