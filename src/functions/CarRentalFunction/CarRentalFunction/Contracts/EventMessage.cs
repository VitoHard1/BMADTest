namespace CarRentalFunction.Contracts;

public sealed class EventMessage
{
    public Guid Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public EventType Type { get; init; }
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public enum EventType
{
    PageView = 0,
    Click = 1,
    Purchase = 2
}
