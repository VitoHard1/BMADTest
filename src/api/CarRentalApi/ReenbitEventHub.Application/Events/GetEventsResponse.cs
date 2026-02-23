namespace ReenbitEventHub.Application.Events;

// BE-02: paged response contract for GET /api/events.
public sealed class GetEventsResponse
{
    public IReadOnlyCollection<EventResponse> Items { get; init; } = Array.Empty<EventResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
