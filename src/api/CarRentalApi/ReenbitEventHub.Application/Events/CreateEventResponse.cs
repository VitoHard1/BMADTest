namespace ReenbitEventHub.Application.Events;

public sealed class CreateEventResponse
{
    public int PublishedCount { get; init; }
    public IReadOnlyList<Guid> EventIds { get; init; } = Array.Empty<Guid>();
}
