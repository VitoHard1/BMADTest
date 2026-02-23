using Microsoft.AspNetCore.Mvc;
using ReenbitEventHub.Application.Events;

namespace CarRentalApi.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController(IEventApplicationService eventService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CreateEventResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateEventResponse>> Create([FromBody] CreateEventRequest request, CancellationToken cancellationToken)
    {
        var response = await eventService.CreateAsync(request, cancellationToken);
        return Accepted(response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(GetEventsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GetEventsResponse>> GetAll(
        [FromQuery] GetEventsQueryRequest query,
        CancellationToken cancellationToken)
    {
        // BE-02: return paged query response (items/totalCount/page/pageSize).
        var response = await eventService.GetAsync(query, cancellationToken);
        return Ok(response);
    }
}
