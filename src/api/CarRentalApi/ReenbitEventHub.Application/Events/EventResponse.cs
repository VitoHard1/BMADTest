using ReenbitEventHub.Domain.Enums;

namespace ReenbitEventHub.Application.Events;

public sealed class EventResponse
{
    public Guid Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public EventType Type { get; init; }
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
