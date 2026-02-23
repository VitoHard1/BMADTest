using ReenbitEventHub.Domain.Enums;

namespace ReenbitEventHub.Application.Events;

public sealed class CreateEventCommand
{
    public string UserId { get; init; } = string.Empty;
    public EventType Type { get; init; }
    public string Description { get; init; } = string.Empty;
}
