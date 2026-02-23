using System.ComponentModel.DataAnnotations;
using ReenbitEventHub.Domain.Enums;

namespace CarRentalApi.Contracts;

public class CreateEventRequest
{
    [Required]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public EventType Type { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
}
