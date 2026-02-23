using ReenbitEventHub.Domain.Enums;

namespace ReenbitEventHub.Domain.Entities;

public class Event
{
    private Event()
    {
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public EventType Type { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    public static Event Create(string userId, EventType type, string description, DateTime createdAtUtc)
    {
        return new Event
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Description = description,
            CreatedAt = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)
        };
    }

    public static Event Reconstitute(Guid id, string userId, EventType type, string description, DateTime createdAtUtc)
    {
        return new Event
        {
            Id = id,
            UserId = userId,
            Type = type,
            Description = description,
            CreatedAt = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)
        };
    }
}
