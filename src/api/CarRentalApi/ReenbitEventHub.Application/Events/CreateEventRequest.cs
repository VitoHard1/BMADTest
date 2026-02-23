using System.ComponentModel.DataAnnotations;

namespace ReenbitEventHub.Application.Events;

public class CreateEventRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [EnumDataType(typeof(EventAction))]
    public EventAction Action { get; set; }

    [Required]
    public string CarId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}
