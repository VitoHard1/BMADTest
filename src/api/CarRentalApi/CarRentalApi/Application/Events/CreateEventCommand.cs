using ReenbitEventHub.Domain.Enums;

namespace CarRentalApi.Application.Events;

public sealed class CreateEventCommand
{
    public string UserId { get; init; } = string.Empty;
    public EventType Type { get; init; }
    public string Description { get; init; } = string.Empty;
}
