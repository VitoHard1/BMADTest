using ReenbitEventHub.Domain.Enums;

namespace ReenbitEventHub.Domain.DTOs;

public class EventMessage
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public EventType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
